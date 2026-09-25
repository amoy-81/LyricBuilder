namespace LyricBuilder.Domain.Entities;

/// <summary>
/// A person's public profile — the author of their lyrics.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="Entities.Account"/> so that credentials never travel with the
/// profile: anything that loads a user to show an author's name has no password hash in hand.
/// </remarks>
public class User : BaseEntity
{
    /// <summary>Display name, shown as the author.</summary>
    public string Name { get; set; } = string.Empty;

    public Account Account { get; set; } = null!;

    public ICollection<Lyric> Lyrics { get; set; } = [];
}
