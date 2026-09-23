namespace LyricBuilder.Domain.Enums;

/// <summary>
/// What a <see cref="Entities.Tag"/> describes. Style says how a lyric sounds; tags say how it
/// feels and what it is about.
/// </summary>
public enum TagCategory
{
    /// <summary>Emotional colour: melancholic, defiant, playful.</summary>
    Mood = 0,

    /// <summary>Subject matter: love, migration, city life.</summary>
    Theme = 1
}
