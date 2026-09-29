using System.Text;

namespace LyricBuilder.Core.Services.LyricSections;

/// <summary>
/// The prompt for writing one section: fixed rules in the system prompt, everything about this
/// song and section in the user message.
/// </summary>
/// <remarks>
/// The user message is split into tagged blocks so the model cannot mistake an example for the
/// writer's draft, or the writer's instruction for part of the song.
/// </remarks>
internal static class SectionPrompt
{
    /// <summary>The reply's lyric text sits between these; anything outside them is ignored.</summary>
    public const string LyricsOpenTag = "<lyrics>";

    public const string LyricsCloseTag = "</lyrics>";

    public const string System = """
        You are an accomplished songwriter. A writer is building a song one section at a time,
        and you write or rework the one section they ask for.

        Your standard is the reference examples. They are real, human-written sections of the
        same type, style and language, picked by a curator because they are good. Study them
        before you write:
        - how many lines they have, and how long the lines are;
        - how dense the rhymes are and where they fall, at line ends and inside lines;
        - the register of the language: colloquial or literary, plain or ornate;
        - what kind of imagery they use, and how concrete it is.
        Write a section that would sit naturally among them. Learn their craft, but never reuse
        their lines, phrases or signature images.

        Rules:
        - Write only in the song's language and script. Do not translate or transliterate.
        - Serve this song: keep its concept, its voice (who speaks, to whom), its tense and its
          story continuous with the other sections. Do not repeat their lines unless asked.
        - Follow the style guidelines, the rhyme scheme and the brief when they are given. The
          writer's instruction outranks all of them.
        - Make it singable: parallel lines have similar syllable counts, stresses fall
          naturally, and no word is there only to force a rhyme.
        - Prefer concrete, specific images to abstract statements and cliches.
        - Fit the section's role. A chorus or hook states the song's central idea in few,
          memorable, repeatable words. A verse moves the story on with detail. A pre-chorus
          builds toward the chorus. A bridge brings a new angle.

        A current draft, when given, is the writer's own work. Keep their voice and their
        strongest lines, and change only what the task calls for.

        Reply with the whole section between <lyrics> and </lyrics>, one lyric line per line,
        and nothing outside the tags:
        <lyrics>
        first line
        second line
        </lyrics>
        Inside the tags go only lyric lines: no title, no section label such as [Verse], no
        notes or explanations, no quotation marks around lines.
        """;

    public static string BuildUserMessage(
        Lyric lyric,
        LyricSection target,
        string? styleGuidelines,
        IReadOnlyList<string> examples,
        string? instruction)
    {
        var message = new StringBuilder();

        AppendSong(message, lyric);

        if (styleGuidelines.IsNotNullOrEmpty())
            AppendBlock(message, "style_guidelines", styleGuidelines);

        AppendSection(message, target);
        AppendSongSoFar(message, lyric, target);
        AppendExamples(message, target.Type, examples);

        if (target.Content.IsNotNullOrEmpty())
            AppendBlock(message, "current_draft", target.Content);

        if (instruction.IsNotNullOrEmpty())
            AppendBlock(message, "writer_instruction", instruction);

        AppendBlock(message, "task", DescribeTask(target, instruction));

        return message.ToString();
    }

    private static void AppendSong(StringBuilder message, Lyric lyric)
    {
        var song = new StringBuilder();
        song.AppendLine($"Title: {lyric.Title}");
        song.AppendLine($"Style: {lyric.Style.Name}"
                        + (lyric.Style.Description.IsNotNullOrEmpty() ? $" — {lyric.Style.Description}" : ""));
        AppendTags(song, "Moods", lyric, TagCategory.Mood);
        AppendTags(song, "Themes", lyric, TagCategory.Theme);
        song.AppendLine($"Language: {(lyric.Language.IsNotNullOrEmpty() ? lyric.Language : "the language of the draft and concept")}");
        if (lyric.Bpm is { } bpm)
            song.AppendLine($"Tempo: {bpm} BPM");
        if (lyric.Concept.IsNotNullOrEmpty())
            song.AppendLine($"Concept: {lyric.Concept}");

        AppendBlock(message, "song", song.ToString());
    }

    private static void AppendTags(StringBuilder song, string label, Lyric lyric, TagCategory category)
    {
        var names = lyric.Tags.Where(t => t.Category == category).Select(t => t.Name).ToList();
        if (names.Count > 0)
            song.AppendLine($"{label}: {string.Join(", ", names)}");
    }

    private static void AppendSection(StringBuilder message, LyricSection target)
    {
        var section = new StringBuilder();
        section.AppendLine($"Type: {target.Type}");
        if (target.RhymeScheme.IsNotNullOrEmpty())
            section.AppendLine($"Rhyme scheme: {target.RhymeScheme}");
        if (target.Brief.IsNotNullOrEmpty())
            section.AppendLine($"Brief: {target.Brief}");

        AppendBlock(message, "section_to_write", section.ToString());
    }

    /// <summary>
    /// The whole song in order, so the section fits what comes before and after it. The target
    /// is marked rather than shown; its text, if any, goes in the draft block.
    /// </summary>
    private static void AppendSongSoFar(StringBuilder message, Lyric lyric, LyricSection target)
    {
        var ordered = lyric.GetSectionsInOrder();
        if (ordered.Count == 1)
            return;

        var song = new StringBuilder();
        var countByType = new Dictionary<SectionType, int>();

        foreach (var section in ordered)
        {
            countByType[section.Type] = countByType.GetValueOrDefault(section.Type) + 1;
            var label = $"{section.Type} {countByType[section.Type]}";

            if (section == target)
                song.AppendLine($"[{label}] <<< the section to write >>>");
            else if (section.IsRepeat)
                song.AppendLine($"[{label}] (repeats an earlier {section.Type})");
            else
                song.AppendLine($"[{label}]\n{(section.Content.IsNotNullOrEmpty() ? section.Content : "(not written yet)")}");

            song.AppendLine();
        }

        AppendBlock(message, "song_so_far", song.ToString());
    }

    private static void AppendExamples(StringBuilder message, SectionType type, IReadOnlyList<string> examples)
    {
        if (examples.Count == 0)
        {
            AppendBlock(message, "reference_examples",
                $"No reference {type} sections exist for this style yet. Rely on the style guidelines.");
            return;
        }

        var block = new StringBuilder();
        for (var i = 0; i < examples.Count; i++)
            block.AppendLine($"<example n=\"{i + 1}\">\n{examples[i].Trim()}\n</example>");

        AppendBlock(message, "reference_examples", block.ToString());
    }

    private static string DescribeTask(LyricSection target, string? instruction)
    {
        if (target.Content.IsNullOrEmpty())
            return instruction.IsNotNullOrEmpty()
                ? $"Write this {target.Type} from scratch, as the writer's instruction asks."
                : $"Write this {target.Type} from scratch.";

        return instruction.IsNotNullOrEmpty()
            ? "Rework the current draft as the writer's instruction asks. Change what it asks for "
              + "and keep the rest. Return the whole section."
            : "Polish and complete the current draft: finish unfinished lines, bring it to the "
              + "length of the examples, tighten the rhymes and rhythm, and sharpen weak images. "
              + "Keep the writer's ideas and best lines. Return the whole section.";
    }

    private static void AppendBlock(StringBuilder message, string tag, string content) =>
        message.Append('<').Append(tag).AppendLine(">")
            .AppendLine(content.Trim())
            .Append("</").Append(tag).AppendLine(">")
            .AppendLine();
}
