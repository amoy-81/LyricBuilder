using LyricBuilder.Core.Models.Tags;
using Microsoft.EntityFrameworkCore;

namespace LyricBuilder.Core.Services.Tags;

public interface IAdminTagService
{
    Task<StatefulPagedResult<TagModel>> GetTagsAsync(AdminTagFilter filter, CancellationToken ct);
    Task<StatefulResult<TagModel>> GetTagByIdAsync(Guid id, CancellationToken ct);
    Task<MutationOperationResult> CreateTagAsync(TagMutation mutation, CancellationToken ct);
    Task<MutationOperationResult> UpdateTagAsync(Guid id, TagMutation mutation, CancellationToken ct);
    Task<MutationOperationResult> DeleteTagAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Maintains the mood and theme tags. Admins only; writers read them through
/// <see cref="TagService"/>.
/// </summary>
/// <remarks>
/// Admins see every tag, inactive ones included. The admin check is made here as well as on the
/// controller, so no other caller of the service can reach the tags by skipping the endpoint.
/// </remarks>
public sealed class AdminTagService(
    ILogger<AdminTagService> logger,
    IRepository<Tag> tagRepository,
    IUnitOfWork unitOfWork,
    RequestContext requestContext) : IAdminTagService
{
    public async Task<StatefulPagedResult<TagModel>> GetTagsAsync(AdminTagFilter filter, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return StatefulPagedResult<TagModel>.Failed(AdminOnly());

            var query = tagRepository.Query();

            if (filter.IsActive is not null)
                query = query.Where(t => t.IsActive == filter.IsActive);

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
            logger.LogError(e, "error while getting tags for admin");
            return StatefulPagedResult<TagModel>.Failed(
                InternalError.InternalServerError("error while getting tags"));
        }
    }

    public async Task<StatefulResult<TagModel>> GetTagByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return StatefulResult<TagModel>.Failed(AdminOnly());

            var tag = await tagRepository.GetByIdAsync(id, ct);
            if (tag is null)
                return StatefulResult<TagModel>.Failed(TagNotFound());

            return StatefulResult<TagModel>.Success(TagModel.Map(tag));
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting tag {TagId} for admin", id);
            return StatefulResult<TagModel>.Failed(
                InternalError.InternalServerError("error while getting tag"));
        }
    }

    public async Task<MutationOperationResult> CreateTagAsync(TagMutation mutation, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return MutationOperationResult.Failed(AdminOnly());

            var category = mutation.Category!.Value;
            if (await tagRepository.AnyAsync(t => t.Category == category && t.Slug == mutation.Slug, ct))
                return MutationOperationResult.Failed(SlugTaken(category, mutation.Slug));

            var tag = new Tag
            {
                Name = mutation.Name.Trim(),
                Slug = mutation.Slug,
                Category = category,
                IsActive = mutation.IsActive
            };

            await tagRepository.AddAsync(tag, ct);
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(tag.Id, "tag created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while creating tag");
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while creating tag"));
        }
    }

    public async Task<MutationOperationResult> UpdateTagAsync(Guid id, TagMutation mutation, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return MutationOperationResult.Failed(AdminOnly());

            var tag = await tagRepository.GetByIdAsync(id, ct);
            if (tag is null)
                return MutationOperationResult.Failed(TagNotFound());

            // Prompts and clients refer to tags by slug, so renaming one would break them.
            if (mutation.Slug != tag.Slug)
                return MutationOperationResult.Failed(InternalError.BadRequest(
                    $"slug of tag {id} cannot change from '{tag.Slug}' to '{mutation.Slug}'",
                    "a tag's slug cannot be changed"));

            // The slug is unique per category, so moving to another category can collide.
            var category = mutation.Category!.Value;
            if (category != tag.Category
                && await tagRepository.AnyAsync(t => t.Category == category && t.Slug == tag.Slug, ct))
                return MutationOperationResult.Failed(SlugTaken(category, tag.Slug));

            tag.Name = mutation.Name.Trim();
            tag.Category = category;
            tag.IsActive = mutation.IsActive;

            tagRepository.Update(tag);
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(tag.Id, "tag updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while updating tag {TagId}", id);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while updating tag"));
        }
    }

    /// <summary>
    /// Soft-deletes the tag. Lyrics that carry it keep their <c>LyricTags</c> rows, but the query
    /// filter hides the deleted tag, so it simply drops off them.
    /// </summary>
    public async Task<MutationOperationResult> DeleteTagAsync(Guid id, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return MutationOperationResult.Failed(AdminOnly());

            var tag = await tagRepository.GetByIdAsync(id, ct);
            if (tag is null)
                return MutationOperationResult.Failed(TagNotFound());

            tagRepository.Remove(tag);
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(id, "tag deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while deleting tag {TagId}", id);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while deleting tag"));
        }
    }

    private static InternalError TagNotFound() => InternalError.NotFound("tag not found");

    private static InternalError SlugTaken(TagCategory category, string slug) =>
        InternalError.Conflict(
            $"tag slug '{slug}' is taken in category {category}",
            "a tag with this slug already exists in this category");

    private static InternalError AdminOnly() =>
        InternalError.Forbidden("tags are maintained by admins only");
}
