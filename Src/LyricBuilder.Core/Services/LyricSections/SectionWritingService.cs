using System.Text.RegularExpressions;
using LyricBuilder.Abstractions.Domain.Exceptions;
using LyricBuilder.Core.Ai;
using LyricBuilder.Core.Models.LyricSections;
using Microsoft.EntityFrameworkCore;

namespace LyricBuilder.Core.Services.LyricSections;

public interface ISectionWritingService
{
    Task<StatefulResult<LyricSectionModel>> EditWithAiAsync(
        Guid lyricId, Guid sectionId, SectionAiEditMutation mutation, CancellationToken ct);
}

/// <summary>
/// Writes or reworks a section of the caller's lyric with the AI, and saves the result.
/// </summary>
/// <remarks>
/// What keeps the output from being generic is the reference examples, not the model: every
/// request shows it curated sections of the same type, style and language, ranked by how many
/// moods and themes they share with the lyric. Only human-written references are used, so the
/// model never learns from its own output.
/// </remarks>
public sealed class SectionWritingService(
    ILogger<SectionWritingService> logger,
    IRepository<Lyric> lyricRepository,
    IRepository<LyricSection> sectionRepository,
    IRepository<Style> styleRepository,
    IChatCompletionClient chatClient,
    IUnitOfWork unitOfWork,
    RequestContext requestContext) : ISectionWritingService
{
    /// <summary>Enough to show a pattern without drowning out the brief.</summary>
    private const int MaxExamples = 4;

    /// <summary>Read per style, so the examples can be spread over different songs.</summary>
    private const int CandidatesPerStyle = 20;

    /// <summary>The same limit a writer's own text has.</summary>
    private const int MaxContentLength = 4000;

    public async Task<StatefulResult<LyricSectionModel>> EditWithAiAsync(
        Guid lyricId, Guid sectionId, SectionAiEditMutation mutation, CancellationToken ct)
    {
        try
        {
            // One round trip for everything the prompt needs from the lyric. The join returns
            // sections × tags rows, which stays small: a lyric has a handful of each.
            var lyric = await lyricRepository.QueryTracked()
                .Include(l => l.Sections)
                .Include(l => l.Style).ThenInclude(s => s.ParentStyle)
                .Include(l => l.Tags)
                .AsSingleQuery()
                .FirstOrDefaultAsync(l => l.Id == lyricId && l.AuthorId == requestContext.UserId, ct);
            if (lyric is null)
                return StatefulResult<LyricSectionModel>.Failed(InternalError.NotFound("lyric not found"));

            var section = lyric.Sections.FirstOrDefault(s => s.Id == sectionId);
            if (section is null)
                return StatefulResult<LyricSectionModel>.Failed(InternalError.NotFound("section not found"));

            // Before the model call, so a request that cannot be saved costs no tokens.
            section.EnsureNotRepeat();

            var styles = await LoadStyleChainAsync(lyric.Style, ct);
            var guidelines = styles.Select(s => s.WritingGuidelines).FirstOrDefault(g => g.IsNotNullOrEmpty());
            var examples = await FindExamplesAsync(lyric, section.Type, styles, ct);

            var userMessage = SectionPrompt.BuildUserMessage(lyric, section, guidelines, examples, mutation.Instruction);
            var reply = await chatClient.CompleteAsync(SectionPrompt.System, userMessage, ct);

            section.ApplyGeneratedContent(ReadContent(reply));
            await unitOfWork.SaveChangesAsync(ct);

            return StatefulResult<LyricSectionModel>.Success(LyricSectionModel.Map(section));
        }
        catch (LyricBuilderException e)
        {
            return StatefulResult<LyricSectionModel>.Failed(e.ToInternalError());
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while writing section {SectionId} of lyric {LyricId} with AI", sectionId, lyricId);
            return StatefulResult<LyricSectionModel>.Failed(
                InternalError.InternalServerError("error while writing section with AI"));
        }
    }

    /// <summary>The lyric's style followed by its ancestors, nearest first.</summary>
    private async Task<IReadOnlyList<Style>> LoadStyleChainAsync(Style style, CancellationToken ct)
    {
        // Always ends: a style cannot be nested under itself or one of its own sub-styles.
        var chain = new List<Style> { style };
        while (chain[^1].ParentStyleId is { } parentId)
        {
            // The parent came with the lyric; only deeper ancestors cost a query each.
            var parent = chain[^1].ParentStyle
                         ?? await styleRepository.FirstOrDefaultAsync(s => s.Id == parentId, ct);
            if (parent is null)
                break;

            chain.Add(parent);
        }

        return chain;
    }

    /// <summary>
    /// Reference sections to show the model, best first. The lyric's own style comes first;
    /// a parent style's examples only make up a shortfall.
    /// </summary>
    private async Task<IReadOnlyList<string>> FindExamplesAsync(
        Lyric lyric, SectionType type, IReadOnlyList<Style> styles, CancellationToken ct)
    {
        var tagIds = lyric.Tags.Select(t => t.Id).ToList();
        var candidates = new List<ReferenceSection>();

        foreach (var style in styles)
        {
            var query = sectionRepository.Query()
                .Where(s => s.Type == type
                            && s.Origin == ContentOrigin.Human
                            && s.Content != ""
                            && s.LyricId != lyric.Id
                            && s.Lyric.Kind == LyricKind.Reference
                            && s.Lyric.StyleId == style.Id);

            // Examples in another language teach the model nothing about rhyme or register.
            if (lyric.Language.IsNotNullOrEmpty())
                query = query.Where(s => s.Lyric.Language == lyric.Language);

            // Id breaks ties, so the same lyric gets the same examples every time.
            candidates.AddRange(await query
                .OrderByDescending(s => s.Lyric.Tags.Count(t => tagIds.Contains(t.Id)))
                .ThenBy(s => s.Id)
                .Take(CandidatesPerStyle)
                .Select(s => new ReferenceSection(s.Id, s.LyricId, s.Content))
                .ToListAsync(ct));

            if (candidates.DistinctBy(c => c.LyricId).Count() >= MaxExamples)
                break;
        }

        // One section per song first, so the model sees a range of writing rather than one
        // song's habits; then fill up with the best of the rest.
        return candidates
            .DistinctBy(c => c.LyricId)
            .Concat(candidates)
            .DistinctBy(c => c.Id)
            .Take(MaxExamples)
            .Select(c => c.Content)
            .ToList();
    }

    /// <summary>
    /// The section text from between the reply's lyrics tags, one trimmed line per line. A reply
    /// without the closing tag was cut off, so it is refused rather than saved half-written.
    /// </summary>
    private string ReadContent(string reply)
    {
        var start = reply.IndexOf(SectionPrompt.LyricsOpenTag, StringComparison.Ordinal);
        var end = reply.LastIndexOf(SectionPrompt.LyricsCloseTag, StringComparison.Ordinal);
        var content = start >= 0 && end > start
            ? reply[(start + SectionPrompt.LyricsOpenTag.Length)..end]
            : null;

        if (content.IsNullOrEmpty())
        {
            // The reply is logged, not put in the exception: its message reaches the client.
            logger.LogWarning("model reply has no usable content: {Reply}", reply);
            throw new LyricBuilderException("model reply has no usable content",
                friendlyMessage: "the AI writer returned an unusable answer, try again");
        }

        // Models add a label such as "[Verse]" now and then despite the prompt.
        var lines = content.ReplaceLineEndings("\n").Trim().Split('\n')
            .Select(line => line.Trim())
            .Where(line => !SectionLabel.IsMatch(line));
        var text = string.Join('\n', lines);

        if (text.Length > MaxContentLength)
            throw new LyricBuilderException($"model reply is {text.Length} characters long",
                friendlyMessage: "the AI writer returned too long a section, try again");

        return text;
    }

    /// <summary>A whole line that is only a label, such as <c>[Verse 1]</c>.</summary>
    private static readonly Regex SectionLabel = new(@"^\[[^\]]*\]$", RegexOptions.Compiled);

    private sealed record ReferenceSection(Guid Id, Guid LyricId, string Content);
}
