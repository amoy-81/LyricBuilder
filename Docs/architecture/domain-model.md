# Domain model

The entities are shaped around one future requirement: **generating a lyric one section at a
time, from examples an admin has curated.** Most decisions below follow from it.

```
Style ◄──── Lyric ────► Tag            (many-to-many, via LyricTags)
  ▲           │
  └ parent    └──► LyricSection  (ordered; a section may repeat another)
```

## Lyric

A lyric is a header plus an ordered list of sections. It has **no text column of its own**:
`ComposeText()` assembles the text from the sections.

| Field | Why it exists |
|---|---|
| `StyleId` | Required. The main key for finding examples |
| `Tags` | Moods and themes that narrow the match within a style |
| `Concept` | What the song is about. Context for every section's generation, so the idea holds across sections written days apart |
| `Language` | Examples must be in the same language to be any use |
| `Kind` | `Original` (a writer's lyric) or `Reference` (an admin-curated example) |
| `OriginalArtist` | Attribution, for reference lyrics |

### Reference lyrics use the same structure

Admin examples are ordinary `Lyric` rows with `Kind = Reference`, split into sections like any
other lyric. There is no separate "sample" table.

That is what makes retrieval a plain query. To generate a chorus for a Persian pop lyric:

```csharp
sectionRepository.Query()
    .Where(s => s.Type == SectionType.Chorus
             && s.Lyric.Kind == LyricKind.Reference
             && s.Lyric.StyleId == styleId
             && s.Lyric.Language == "fa-IR")
```

Rank the results by how many tags they share with the target lyric. `IX_Lyrics_Kind_StyleId`
and `IX_LyricSections_Type` exist for this query.

`Kind` is server-owned and absent from `LyricMutation`. A writer cannot turn their own lyric
into training material.

## Style and Tag

Two kinds of descriptor, because they answer different questions:

- **Style** (exactly one per lyric) is *how it sounds*: pop, social rap, ballad. It is a
  curated table rather than free text, so examples and new lyrics agree on the name. It
  carries `WritingGuidelines`, instructions written for the model, not for users. Styles nest
  through `ParentStyleId`: when a sub-style has too few references, fall back to its parent's.
- **Tag** (any number) is *how it feels* (`Mood`) and *what it is about* (`Theme`).

Both have a `Slug`, which is unique among non-deleted rows. Prompts and clients refer to the
slug because it does not change when a name is reworded.

## LyricSection

The unit that gets written or generated at a time.

| Field | Purpose |
|---|---|
| `Type` | Verse, Chorus, Bridge… Examples are retrieved by this |
| `Position` | Zero-based and contiguous. Set only by `Lyric` |
| `Brief` | What this section should say. The input to generation |
| `Content` | The text, newline-separated. Empty until written |
| `RhymeScheme` | e.g. `AABB`. Usable as a constraint in a prompt |
| `Origin` | `Human`, `AiGenerated`, `AiEdited` |

### Order is kept by the lyric

`AddSection`, `AddRepeat`, `MoveSection` and `RemoveSection` live on `Lyric` and renumber after
every change, so positions never have gaps or duplicates. They work on the loaded collection:
**load the lyric with its sections (tracked) before calling them**, or the new positions will
be computed against an empty list.

`(LyricId, Position)` is indexed but **not unique**. A move renumbers several rows in one save,
and a unique index would reject the intermediate states.

Removing a section is a **hard delete**, unlike the lyric's soft delete. A section has no
meaning outside its lyric.

### Repeats

The second and later choruses are repeats: `RepeatsSectionId` points at the original, and the
repeat carries no text of its own. Editing the chorus once updates it everywhere it recurs,
and generation never produces three slightly different choruses.

A repeat always points at the original, never at another repeat. Removing an original removes
its repeats too.

### Origin

Record `Origin` honestly. When examples are selected, prefer `Human` sections: a model that
learns from its own unreviewed output drifts.

## Not modelled yet

- **Generation history.** Candidate texts per section, the prompt used, which one the writer
  accepted. That would be a `SectionDraft` child of `LyricSection` when it is needed.
- **Line-level structure.** `Content` is one string. Syllable counts or per-line rhyme would
  need a `Line` entity.
- **Authors as entities.** `AuthorId` is still a bare Guid.
