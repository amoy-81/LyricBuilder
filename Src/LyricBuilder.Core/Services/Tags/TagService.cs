using LyricBuilder.Core.Models.Tags;
using Microsoft.EntityFrameworkCore;

namespace LyricBuilder.Core.Services.Tags;

public interface ITagService
{
    Task<StatefulPagedResult<TagModel>> GetTagsAsync(TagFilter filter, CancellationToken ct);
    Task<StatefulResult<TagModel>> GetTagByIdAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// The mood and theme tags as writers see it: read-only and active tags only. Admins maintain
/// them through <see cref="AdminTagService"/>.
/// </summary>
public sealed class TagService(
    ILogger<TagService> logger,
    IRepository<Tag> tagRepository) : ITagService
{
    public async Task<StatefulPagedResult<TagModel>> GetTagsAsync(TagFilter filter, CancellationToken ct)
    {
        try
        {
            var query = tagRepository.Query()
                .Where(t => t.IsActive);

            if (filter.Category is not null)
                query = query.Where(t => t.Category == filter.Category);

            if (filter.Name.IsNotNullOrEmpty())
                query = query.Where(t => t.Name.Contains(filter.Name));

            var total = await query.LongCountAsync(ct);
            var tags = await query
                .OrderBy(t => t.Category)
                .ThenBy(t => t.Name)
                .Skip(filter.Skip)
                .Take(filter.PageSize)
                .ToListAsync(ct);

            return StatefulPagedResult<TagModel>.Success(tags.Select(TagModel.Map).ToList(), total);
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting tags");
            return StatefulPagedResult<TagModel>.Failed(
                InternalError.InternalServerError("error while getting tags"));
        }
    }

    public async Task<StatefulResult<TagModel>> GetTagByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            // An inactive tag reads as not found, the same as a deleted one.
            var tag = await tagRepository.FirstOrDefaultAsync(t => t.Id == id && t.IsActive, ct);
            if (tag is null)
                return StatefulResult<TagModel>.Failed(InternalError.NotFound("tag not found"));

            return StatefulResult<TagModel>.Success(TagModel.Map(tag));
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting tag {TagId}", id);
            return StatefulResult<TagModel>.Failed(
                InternalError.InternalServerError("error while getting tag"));
        }
    }
}
