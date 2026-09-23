using LyricBuilder.Abstractions.Domain.Exceptions;
using LyricBuilder.Domain.Enums;

namespace LyricBuilder.Domain.Entities;

/// <summary>
/// A song lyric: an ordered sequence of <see cref="LyricSection"/>s written in one
/// <see cref="Entities.Style"/>.
/// </summary>
/// <remarks>
/// A lyric is built section by section, not in one pass. Section order is an invariant of the
/// lyric, so sections are added, moved and removed only through the methods here, which keep
/// positions contiguous. They work on the loaded <see cref="Sections"/>, so load those first.
/// <para>
/// Admin-curated examples are lyrics too (<see cref="LyricKind.Reference"/>), with the same
/// structure. That is what lets generation ask for "choruses in this style, in this language"
/// with an ordinary query.
/// </para>
/// </remarks>
public class Lyric : BaseEntity
{
    private readonly List<LyricSection> _sections = [];

    public string Title { get; set; } = string.Empty;

    /// <summary>Owning writer. Not a navigation yet — the identity model is not in place.</summary>
    public Guid AuthorId { get; set; }

    public LyricKind Kind { get; set; } = LyricKind.Original;

    public LyricStatus Status { get; set; } = LyricStatus.Draft;

    public Guid StyleId { get; set; }

    public Style Style { get; set; } = null!;

    /// <summary>Moods and themes that refine the style.</summary>
    public ICollection<Tag> Tags { get; set; } = [];

    /// <summary>
    /// What the song is about, in the writer's words. Given as context to every section's
    /// generation, so the idea holds across sections written at different times.
    /// </summary>
    public string? Concept { get; set; }

    /// <summary>Written language as a BCP-47 tag (e.g. <c>fa-IR</c>, <c>en-US</c>).</summary>
    public string? Language { get; set; }

    /// <summary>Intended tempo in beats per minute, when the writer has one in mind.</summary>
    public int? Bpm { get; set; }

    /// <summary>Performer of the original song. Reference lyrics only, for attribution.</summary>
    public string? OriginalArtist { get; set; }

    public DateTime? PublishedAt { get; set; }

    /// <summary>Unordered; use <see cref="GetSectionsInOrder"/> for performance order.</summary>
    public IReadOnlyCollection<LyricSection> Sections => _sections;

    public IReadOnlyList<LyricSection> GetSectionsInOrder() =>
        _sections.OrderBy(s => s.Position).ToList();

    /// <summary>Adds an empty section, at the end unless <paramref name="position"/> is given.</summary>
    public LyricSection AddSection(SectionType type, string? brief = null, int? position = null)
    {
        var section = new LyricSection { LyricId = Id, Type = type, Brief = brief };
        Insert(section, position);
        return section;
    }

    /// <summary>
    /// Repeats an existing section — typically a chorus — without copying its text, so an edit
    /// to the original shows up everywhere it recurs.
    /// </summary>
    public LyricSection AddRepeat(Guid sectionId, int? position = null)
    {
        var source = FindSection(sectionId);
        var original = source.RepeatsSectionId is { } originalId ? FindSection(originalId) : source;

        var repeat = new LyricSection
        {
            LyricId = Id,
            Type = original.Type,
            RepeatsSectionId = original.Id
        };
        Insert(repeat, position);
        return repeat;
    }

    public void MoveSection(Guid sectionId, int newPosition)
    {
        var section = FindSection(sectionId);
        var ordered = GetSectionsInOrder().ToList();
        EnsurePositionInRange(newPosition, ordered.Count - 1);

        ordered.Remove(section);
        ordered.Insert(newPosition, section);
        Renumber(ordered);
    }

    /// <summary>
    /// Removes a section and every repeat of it. Sections have no life outside their lyric, so
    /// this is a hard delete once saved — unlike the lyric's own soft delete.
    /// </summary>
    public void RemoveSection(Guid sectionId)
    {
        var section = FindSection(sectionId);

        _sections.RemoveAll(s => s == section || s.RepeatsSectionId == section.Id);
        Renumber(GetSectionsInOrder());
    }

    /// <summary>
    /// The full text in performance order, repeats resolved, sections separated by a blank line.
    /// </summary>
    public string ComposeText()
    {
        var byId = _sections.ToDictionary(s => s.Id);

        return string.Join("\n\n", GetSectionsInOrder()
            .Select(s => s.RepeatsSectionId is { } id && byId.TryGetValue(id, out var original)
                ? original.Content
                : s.Content)
            .Where(content => content.Length > 0));
    }

    private void Insert(LyricSection section, int? position)
    {
        var ordered = GetSectionsInOrder().ToList();
        var index = position ?? ordered.Count;
        EnsurePositionInRange(index, ordered.Count);

        ordered.Insert(index, section);
        _sections.Add(section);
        Renumber(ordered);
    }

    private LyricSection FindSection(Guid sectionId) =>
        _sections.FirstOrDefault(s => s.Id == sectionId)
        ?? throw LyricBuilderException.NotFound(
            $"section {sectionId} not found on lyric {Id}", "section not found");

    private static void EnsurePositionInRange(int position, int max)
    {
        if (position < 0 || position > max)
            throw LyricBuilderException.BadRequest(
                $"position {position} is outside 0..{max}", "invalid section position");
    }

    private static void Renumber(IReadOnlyList<LyricSection> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].Position = i;
    }
}
