using LyricBuilder.Abstractions.Domain.Exceptions;
using LyricBuilder.Core.Models.LyricSections;
using Microsoft.EntityFrameworkCore;

namespace LyricBuilder.Core.Services.LyricSections;

public interface ILyricSectionService
{
    Task<StatefulResult<IReadOnlyList<LyricSectionModel>>> GetSectionsAsync(Guid lyricId, CancellationToken ct);
    Task<MutationOperationResult> CreateSectionAsync(Guid lyricId, LyricSectionMutation mutation, CancellationToken ct);
    Task<MutationOperationResult> UpdateSectionAsync(Guid lyricId, Guid sectionId, LyricSectionMutation mutation, CancellationToken ct);
    Task<MutationOperationResult> DeleteSectionAsync(Guid lyricId, Guid sectionId, CancellationToken ct);
}

/// <summary>
/// Reads and writes the sections of the caller's own lyrics.
/// </summary>
/// <remarks>
/// Order is kept by <see cref="Lyric"/>, so every write loads the lyric with its sections,
/// tracked, and goes through its methods rather than touching positions here. Those methods
/// throw <see cref="LyricBuilderException"/> on a broken rule; it is returned as a result.
/// </remarks>
public sealed class LyricSectionService(
    ILogger<LyricSectionService> logger,
    IRepository<Lyric> lyricRepository,
    IUnitOfWork unitOfWork,
    RequestContext requestContext) : ILyricSectionService
{
    public async Task<StatefulResult<IReadOnlyList<LyricSectionModel>>> GetSectionsAsync(
        Guid lyricId, CancellationToken ct)
    {
        try
        {
            var lyric = await OwnedLyrics(lyricRepository.Query())
                .FirstOrDefaultAsync(l => l.Id == lyricId, ct);
            if (lyric is null)
                return StatefulResult<IReadOnlyList<LyricSectionModel>>.Failed(LyricNotFound());

            var models = lyric.GetSectionsInOrder().Select(LyricSectionModel.Map).ToList();
            return StatefulResult<IReadOnlyList<LyricSectionModel>>.Success(models);
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting sections of lyric {LyricId}", lyricId);
            return StatefulResult<IReadOnlyList<LyricSectionModel>>.Failed(
                InternalError.InternalServerError("error while getting sections"));
        }
    }

    public async Task<MutationOperationResult> CreateSectionAsync(
        Guid lyricId, LyricSectionMutation mutation, CancellationToken ct)
    {
        try
        {
            var lyric = await LoadOwnedLyricForUpdateAsync(lyricId, ct);
            if (lyric is null)
                return MutationOperationResult.Failed(LyricNotFound());

            var section = lyric.AddSection(mutation.Type!.Value, mutation.Brief, mutation.Position);
            section.RhymeScheme = mutation.RhymeScheme;
            section.EditContent(mutation.Content ?? string.Empty);

            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(section.Id, "section created");
        }
        catch (LyricBuilderException e)
        {
            return MutationOperationResult.Failed(e.ToInternalError());
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while creating a section on lyric {LyricId}", lyricId);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while creating section"));
        }
    }

    public async Task<MutationOperationResult> UpdateSectionAsync(
        Guid lyricId, Guid sectionId, LyricSectionMutation mutation, CancellationToken ct)
    {
        try
        {
            var lyric = await LoadOwnedLyricForUpdateAsync(lyricId, ct);
            if (lyric is null)
                return MutationOperationResult.Failed(LyricNotFound());

            var section = lyric.Sections.FirstOrDefault(s => s.Id == sectionId);
            if (section is null)
                return MutationOperationResult.Failed(InternalError.NotFound("section not found"));

            section.Type = mutation.Type!.Value;
            section.Brief = mutation.Brief;
            section.RhymeScheme = mutation.RhymeScheme;
            section.EditContent(mutation.Content ?? string.Empty);

            if (mutation.Position is { } position && position != section.Position)
                lyric.MoveSection(sectionId, position);

            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(section.Id, "section updated");
        }
        catch (LyricBuilderException e)
        {
            return MutationOperationResult.Failed(e.ToInternalError());
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while updating section {SectionId} of lyric {LyricId}", sectionId, lyricId);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while updating section"));
        }
    }

    public async Task<MutationOperationResult> DeleteSectionAsync(
        Guid lyricId, Guid sectionId, CancellationToken ct)
    {
        try
        {
            var lyric = await LoadOwnedLyricForUpdateAsync(lyricId, ct);
            if (lyric is null)
                return MutationOperationResult.Failed(LyricNotFound());

            // Hard delete, with the section's repeats; the rest are renumbered to close the gap.
            lyric.RemoveSection(sectionId);
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(sectionId, "section deleted");
        }
        catch (LyricBuilderException e)
        {
            return MutationOperationResult.Failed(e.ToInternalError());
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while deleting section {SectionId} of lyric {LyricId}", sectionId, lyricId);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while deleting section"));
        }
    }

    private Task<Lyric?> LoadOwnedLyricForUpdateAsync(Guid lyricId, CancellationToken ct) =>
        OwnedLyrics(lyricRepository.QueryTracked())
            .FirstOrDefaultAsync(l => l.Id == lyricId, ct);

    /// <summary>
    /// The caller's lyrics with their sections. Someone else's lyric is simply absent, so it
    /// reads as not found, the same as in <see cref="Lyrics.LyricService"/>.
    /// </summary>
    private IQueryable<Lyric> OwnedLyrics(IQueryable<Lyric> lyrics) =>
        lyrics
            .Include(l => l.Sections)
            .Where(l => l.AuthorId == requestContext.UserId);

    private static InternalError LyricNotFound() => InternalError.NotFound("lyric not found");
}
