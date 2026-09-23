namespace LyricBuilder.Domain.Enums;

/// <summary>
/// The role a section plays in a song. Generation retrieves reference examples by this type,
/// so a verse is written from other verses and a chorus from other choruses.
/// </summary>
public enum SectionType
{
    Intro = 0,
    Verse = 1,
    PreChorus = 2,
    Chorus = 3,
    PostChorus = 4,
    Hook = 5,
    Bridge = 6,
    Interlude = 7,
    Breakdown = 8,
    Outro = 9
}
