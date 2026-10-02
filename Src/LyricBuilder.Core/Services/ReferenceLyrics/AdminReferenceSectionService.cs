using LyricBuilder.Abstractions.Domain.Exceptions;
using LyricBuilder.Core.Models.LyricSections;
using Microsoft.EntityFrameworkCore;

namespace LyricBuilder.Core.Services.ReferenceLyrics;

public interface IAdminReferenceSectionService
{
    Task<StatefulResult<IReadOnlyList<LyricSectionModel>>> GetSectionsAsync(Guid lyricId, CancellationToken ct);
    Task<MutationOperationResult> CreateSectionAsync(Guid lyricId, LyricSectionMutation mutation, CancellationToken ct);
    Task<MutationOperationResult> UpdateSectionAsync(Guid lyricId, Guid sectionId, LyricSectionMutation mutation, CancellationToken ct);
    Task<MutationOperationResult> DeleteSectionAsync(Guid lyricId, Guid sectionId, CancellationToken ct);
}

/// <summary>
/// Reads and writes the sections of reference lyrics — the examples section writing is shown.
/// Admins only.
/// </summary>
/// <remarks>
/// Works like <see cref="LyricSections.LyricSectionService"/>, scoped to reference lyrics
/// instead of the caller's own: order is kept by <see cref="Lyric"/>, so every write loads the
/// lyric with its sections, tracked, and goes through its methods. Text is written through
/// <see cref="LyricSection.EditContent"/>, so it is recorded as human-written and can be picked
/// as an example.
/// </remarks>
public sealed class AdminReferenceSectionService(
    ILogger<AdminReferenceSectionService> logger,
    IRepository<Lyric> lyricRepository,
    IUnitOfWork unitOfWork,
    RequestContext requestContext) : IAdminReferenceSectionService
{
    public async Task<StatefulResult<IReadOnlyList<LyricSectionModel>>> GetSectionsAsync(
        Guid lyricId, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return StatefulResult<IReadOnlyList<LyricSectionModel>>.Failed(AdminOnly());

            var lyric = await ReferenceLyrics(lyricRepository.Query())
                .FirstOrDefaultAsync(l => l.Id == lyricId, ct);
            if (lyric is null)
                return StatefulResult<IReadOnlyList<LyricSectionModel>>.Failed(LyricNotFound());

            var models = lyric.GetSectionsInOrder().Select(LyricSectionModel.Map).ToList();
            return StatefulResult<IReadOnlyList<LyricSectionModel>>.Success(models);
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting sections of reference lyric {LyricId}", lyricId);
            return StatefulResult<IReadOnlyList<LyricSectionModel>>.Failed(
                InternalError.InternalServerError("error while getting sections"));
        }
    }

    public async Task<MutationOperationResult> CreateSectionAsync(
        Guid lyricId, LyricSectionMutation mutation, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return MutationOperationResult.Failed(AdminOnly());

            var lyric = await LoadReferenceLyricForUpdateAsync(lyricId, ct);
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
            logger.LogError(e, "error while creating a section on reference lyric {LyricId}", lyricId);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while creating section"));
        }
    }

    public async Task<MutationOperationResult> UpdateSectionAsync(
        Guid lyricId, Guid sectionId, LyricSectionMutation mutation, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return MutationOperationResult.Failed(AdminOnly());

            var lyric = await LoadReferenceLyricForUpdateAsync(lyricId, ct);
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
            logger.LogError(e, "error while updating section {SectionId} of reference lyric {LyricId}", sectionId, lyricId);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while updating section"));
        }
    }

    public async Task<MutationOperationResult> DeleteSectionAsync(
        Guid lyricId, Guid sectionId, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return MutationOperationResult.Failed(AdminOnly());

            var lyric = await LoadReferenceLyricForUpdateAsync(lyricId, ct);
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
            logger.LogError(e, "error while deleting section {SectionId} of reference lyric {LyricId}", sectionId, lyricId);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while deleting section"));
        }
    }

    private Task<Lyric?> LoadReferenceLyricForUpdateAsync(Guid lyricId, CancellationToken ct) =>
        ReferenceLyrics(lyricRepository.QueryTracked())
            .FirstOrDefaultAsync(l => l.Id == lyricId, ct);

    /// <summary>
    /// Reference lyrics with their sections. A writer's lyric is simply absent, so it reads as
    /// not found, the same as in <see cref="AdminReferenceLyricService"/>.
    /// </summary>
    private static IQueryable<Lyric> ReferenceLyrics(IQueryable<Lyric> lyrics) =>
        lyrics
            .Include(l => l.Sections)
            .Where(l => l.Kind == LyricKind.Reference);

    private static InternalError LyricNotFound() => InternalError.NotFound("reference lyric not found");

    private static InternalError AdminOnly() =>
        InternalError.Forbidden("reference lyrics are maintained by admins only");
}
