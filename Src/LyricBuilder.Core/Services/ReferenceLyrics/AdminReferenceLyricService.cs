using LyricBuilder.Core.Models.ReferenceLyrics;
using Microsoft.EntityFrameworkCore;

namespace LyricBuilder.Core.Services.ReferenceLyrics;

public interface IAdminReferenceLyricService
{
    Task<StatefulPagedResult<ReferenceLyricModel>> GetReferenceLyricsAsync(ReferenceLyricFilter filter, CancellationToken ct);
    Task<StatefulResult<ReferenceLyricModel>> GetReferenceLyricByIdAsync(Guid id, CancellationToken ct);
    Task<MutationOperationResult> CreateReferenceLyricAsync(ReferenceLyricMutation mutation, CancellationToken ct);
    Task<MutationOperationResult> UpdateReferenceLyricAsync(Guid id, ReferenceLyricMutation mutation, CancellationToken ct);
    Task<MutationOperationResult> DeleteReferenceLyricAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Maintains the reference lyrics that section writing takes its examples from. Admins only.
/// </summary>
/// <remarks>
/// References are ordinary lyrics with <see cref="LyricKind.Reference"/>, so this service sees
/// only those: a writer's lyric reads as not found here, whoever wrote it. Every admin can
/// maintain every reference, not only the ones they curated. The admin check is made here as
/// well as on the controller, so no other caller of the service can reach them by skipping the
/// endpoint.
/// </remarks>
public sealed class AdminReferenceLyricService(
    ILogger<AdminReferenceLyricService> logger,
    IRepository<Lyric> lyricRepository,
    IRepository<Style> styleRepository,
    IRepository<Tag> tagRepository,
    IUnitOfWork unitOfWork,
    RequestContext requestContext) : IAdminReferenceLyricService
{
    public async Task<StatefulPagedResult<ReferenceLyricModel>> GetReferenceLyricsAsync(
        ReferenceLyricFilter filter, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return StatefulPagedResult<ReferenceLyricModel>.Failed(AdminOnly());

            var query = lyricRepository.Query()
                .Where(l => l.Kind == LyricKind.Reference);

            if (filter.StyleId is not null)
                query = query.Where(l => l.StyleId == filter.StyleId);

            if (filter.Language.IsNotNullOrEmpty())
                query = query.Where(l => l.Language == filter.Language);

            if (filter.TagId is not null)
                query = query.Where(l => l.Tags.Any(t => t.Id == filter.TagId));

            if (filter.Title.IsNotNullOrEmpty())
                query = query.Where(l => l.Title.Contains(filter.Title));

            if (filter.OriginalArtist.IsNotNullOrEmpty())
                query = query.Where(l => l.OriginalArtist != null && l.OriginalArtist.Contains(filter.OriginalArtist));

            var total = await query.LongCountAsync(ct);
            var lyrics = await query
                .Include(l => l.Tags)
                .OrderByDescending(l => l.CreatedAt)
                .Skip(filter.Skip)
                .Take(filter.PageSize)
                .ToListAsync(ct);

            var models = lyrics.Select(l => ReferenceLyricModel.Map(l, includeContent: false)).ToList();
            return StatefulPagedResult<ReferenceLyricModel>.Success(models, total);
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting reference lyrics");
            return StatefulPagedResult<ReferenceLyricModel>.Failed(
                InternalError.InternalServerError("error while getting reference lyrics"));
        }
    }

    public async Task<StatefulResult<ReferenceLyricModel>> GetReferenceLyricByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return StatefulResult<ReferenceLyricModel>.Failed(AdminOnly());

            // Content is composed from the sections. The join returns sections × tags rows,
            // which stays small: a lyric has a handful of each.
            var lyric = await lyricRepository.Query()
                .Include(l => l.Sections)
                .Include(l => l.Tags)
                .AsSingleQuery()
                .FirstOrDefaultAsync(l => l.Id == id && l.Kind == LyricKind.Reference, ct);
            if (lyric is null)
                return StatefulResult<ReferenceLyricModel>.Failed(LyricNotFound());

            return StatefulResult<ReferenceLyricModel>.Success(ReferenceLyricModel.Map(lyric));
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting reference lyric {LyricId}", id);
            return StatefulResult<ReferenceLyricModel>.Failed(
                InternalError.InternalServerError("error while getting reference lyric"));
        }
    }

    public async Task<MutationOperationResult> CreateReferenceLyricAsync(
        ReferenceLyricMutation mutation, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin || requestContext.UserId is null)
                return MutationOperationResult.Failed(AdminOnly());

            if (!await styleRepository.AnyAsync(s => s.Id == mutation.StyleId, ct))
                return MutationOperationResult.Failed(StyleNotFound());

            var tags = await LoadTagsAsync(mutation.TagIds, ct);
            if (tags is null)
                return MutationOperationResult.Failed(TagNotFound());

            var lyric = new Lyric
            {
                AuthorId = requestContext.UserId.Value,
                Kind = LyricKind.Reference,
                Tags = tags
            };
            Apply(lyric, mutation);

            await lyricRepository.AddAsync(lyric, ct);
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(lyric.Id, "reference lyric created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while creating reference lyric");
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while creating reference lyric"));
        }
    }

    public async Task<MutationOperationResult> UpdateReferenceLyricAsync(
        Guid id, ReferenceLyricMutation mutation, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return MutationOperationResult.Failed(AdminOnly());

            // Tracked, with its tags, so the tag set can be changed in place.
            var lyric = await lyricRepository.QueryTracked()
                .Include(l => l.Tags)
                .FirstOrDefaultAsync(l => l.Id == id && l.Kind == LyricKind.Reference, ct);
            if (lyric is null)
                return MutationOperationResult.Failed(LyricNotFound());

            if (mutation.StyleId != lyric.StyleId
                && !await styleRepository.AnyAsync(s => s.Id == mutation.StyleId, ct))
                return MutationOperationResult.Failed(StyleNotFound());

            var tags = await LoadTagsAsync(mutation.TagIds, ct);
            if (tags is null)
                return MutationOperationResult.Failed(TagNotFound());

            Apply(lyric, mutation);
            ReplaceTags(lyric, tags);

            // No repository Update: the lyric is tracked, and Update would mark its tags as
            // modified too.
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(lyric.Id, "reference lyric updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while updating reference lyric {LyricId}", id);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while updating reference lyric"));
        }
    }

    /// <summary>
    /// Soft-deletes the reference. Its sections stay in the table, but the lyric's query filter
    /// hides them from example retrieval along with it.
    /// </summary>
    public async Task<MutationOperationResult> DeleteReferenceLyricAsync(Guid id, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return MutationOperationResult.Failed(AdminOnly());

            var lyric = await lyricRepository.GetByIdAsync(id, ct);
            if (lyric is null || lyric.Kind != LyricKind.Reference)
                return MutationOperationResult.Failed(LyricNotFound());

            lyricRepository.Remove(lyric);
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(id, "reference lyric deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while deleting reference lyric {LyricId}", id);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while deleting reference lyric"));
        }
    }

    /// <summary>
    /// The tags with these ids, tracked so they can be attached to a lyric; null when any of
    /// them does not exist.
    /// </summary>
    private async Task<List<Tag>?> LoadTagsAsync(IReadOnlyList<Guid> tagIds, CancellationToken ct)
    {
        var ids = tagIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var tags = await tagRepository.QueryTracked()
            .Where(t => ids.Contains(t.Id))
            .ToListAsync(ct);

        return tags.Count == ids.Count ? tags : null;
    }

    /// <summary>Changes only the difference, so tags kept on the lyric keep their join rows.</summary>
    private static void ReplaceTags(Lyric lyric, IReadOnlyList<Tag> tags)
    {
        var removed = lyric.Tags.Where(t => tags.All(n => n.Id != t.Id)).ToList();
        var added = tags.Where(t => lyric.Tags.All(o => o.Id != t.Id)).ToList();

        foreach (var tag in removed)
            lyric.Tags.Remove(tag);

        foreach (var tag in added)
            lyric.Tags.Add(tag);
    }

    private static void Apply(Lyric lyric, ReferenceLyricMutation mutation)
    {
        lyric.Title = mutation.Title.Trim();
        lyric.StyleId = mutation.StyleId;
        lyric.Concept = mutation.Concept;
        lyric.Language = mutation.Language.Trim();
        lyric.Bpm = mutation.Bpm;
        lyric.OriginalArtist = mutation.OriginalArtist?.Trim();
    }

    private static InternalError LyricNotFound() => InternalError.NotFound("reference lyric not found");

    private static InternalError StyleNotFound() => InternalError.BadRequest("style not found");

    private static InternalError TagNotFound() =>
        InternalError.BadRequest("one or more tags were not found");

    private static InternalError AdminOnly() =>
        InternalError.Forbidden("reference lyrics are maintained by admins only");
}
