using LyricBuilder.Core.Models.Lyrics;
using Microsoft.EntityFrameworkCore;

namespace LyricBuilder.Core.Services.Lyrics;

public interface ILyricService
{
    Task<StatefulPagedResult<LyricModel>> GetLyricsAsync(LyricFilter filter, CancellationToken ct);
    Task<StatefulResult<LyricModel>> GetLyricByIdAsync(Guid id, CancellationToken ct);
    Task<MutationOperationResult> CreateLyricAsync(LyricMutation mutation, CancellationToken ct);
    Task<MutationOperationResult> UpdateLyricAsync(Guid id, LyricMutation mutation, CancellationToken ct);
    Task<MutationOperationResult> DeleteLyricAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Reads and writes lyrics. Picked up automatically by the "Service" naming convention in
/// <see cref="ServiceCollectionExtensions.AddCore"/>.
/// </summary>
public sealed class LyricService(
    ILogger<LyricService> logger,
    IRepository<Lyric> lyricRepository,
    IRepository<Style> styleRepository,
    IUnitOfWork unitOfWork,
    RequestContext requestContext) : ILyricService
{
    public async Task<StatefulPagedResult<LyricModel>> GetLyricsAsync(LyricFilter filter, CancellationToken ct)
    {
        try
        {
            var query = lyricRepository.Query();

            if (filter.Kind is not null)
                query = query.Where(l => l.Kind == filter.Kind);

            if (filter.Status is not null)
                query = query.Where(l => l.Status == filter.Status);

            if (filter.StyleId is not null)
                query = query.Where(l => l.StyleId == filter.StyleId);

            if (filter.Language.IsNotNullOrEmpty())
                query = query.Where(l => l.Language == filter.Language);

            if (filter.Search.IsNotNullOrEmpty())
                query = query.Where(l => l.Title.Contains(filter.Search));

            var total = await query.LongCountAsync(ct);
            var lyrics = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip(filter.Skip)
                .Take(filter.PageSize)
                .ToListAsync(ct);

            var models = lyrics.Select(l => LyricModel.Map(l, includeContent: false)).ToList();
            return StatefulPagedResult<LyricModel>.Success(models, total);
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting lyrics");
            return StatefulPagedResult<LyricModel>.Failed(
                InternalError.InternalServerError("error while getting lyrics"));
        }
    }

    public async Task<StatefulResult<LyricModel>> GetLyricByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            // Content is composed from the sections, so they are loaded with the lyric.
            var lyric = await lyricRepository.Query()
                .Include(l => l.Sections)
                .FirstOrDefaultAsync(l => l.Id == id, ct);
            if (lyric is null)
                return StatefulResult<LyricModel>.Failed(InternalError.NotFound("lyric not found"));

            return StatefulResult<LyricModel>.Success(LyricModel.Map(lyric));
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting lyric {LyricId}", id);
            return StatefulResult<LyricModel>.Failed(
                InternalError.InternalServerError("error while getting lyric"));
        }
    }

    public async Task<MutationOperationResult> CreateLyricAsync(LyricMutation mutation, CancellationToken ct)
    {
        try
        {
            if (requestContext.UserId is null)
                return MutationOperationResult.Failed(InternalError.Unauthorized("no authenticated user"));

            if (!await IsSelectableStyleAsync(mutation.StyleId, ct))
                return MutationOperationResult.Failed(InternalError.BadRequest("style not found or inactive"));

            var lyric = new Lyric
            {
                Title = mutation.Title,
                StyleId = mutation.StyleId,
                Concept = mutation.Concept,
                Language = mutation.Language,
                Bpm = mutation.Bpm,
                AuthorId = requestContext.UserId.Value,
                Kind = LyricKind.Original,
                Status = LyricStatus.Draft
            };

            await lyricRepository.AddAsync(lyric, ct);
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(lyric.Id, "lyric created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while creating lyric");
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while creating lyric"));
        }
    }

    public async Task<MutationOperationResult> UpdateLyricAsync(Guid id, LyricMutation mutation, CancellationToken ct)
    {
        try
        {
            var lyric = await lyricRepository.GetByIdAsync(id, ct);
            if (lyric is null)
                return MutationOperationResult.Failed(InternalError.NotFound("lyric not found"));

            if (lyric.AuthorId != requestContext.UserId)
                return MutationOperationResult.Failed(
                    InternalError.Forbidden("caller does not own this lyric", "you cannot edit this lyric"));

            // Only a change of style is checked, so a style retired later does not lock its
            // existing lyrics out of edits.
            if (mutation.StyleId != lyric.StyleId && !await IsSelectableStyleAsync(mutation.StyleId, ct))
                return MutationOperationResult.Failed(InternalError.BadRequest("style not found or inactive"));

            lyric.Title = mutation.Title;
            lyric.StyleId = mutation.StyleId;
            lyric.Concept = mutation.Concept;
            lyric.Language = mutation.Language;
            lyric.Bpm = mutation.Bpm;

            lyricRepository.Update(lyric);
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(lyric.Id, "lyric updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while updating lyric {LyricId}", id);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while updating lyric"));
        }
    }

    public async Task<MutationOperationResult> DeleteLyricAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var lyric = await lyricRepository.GetByIdAsync(id, ct);
            if (lyric is null)
                return MutationOperationResult.Failed(InternalError.NotFound("lyric not found"));

            if (lyric.AuthorId != requestContext.UserId)
                return MutationOperationResult.Failed(
                    InternalError.Forbidden("caller does not own this lyric", "you cannot delete this lyric"));

            lyricRepository.Remove(lyric);
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(id, "lyric deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while deleting lyric {LyricId}", id);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while deleting lyric"));
        }
    }

    private Task<bool> IsSelectableStyleAsync(Guid styleId, CancellationToken ct) =>
        styleRepository.AnyAsync(s => s.Id == styleId && s.IsActive, ct);
}
