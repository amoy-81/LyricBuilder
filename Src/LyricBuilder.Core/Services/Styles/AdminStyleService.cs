using LyricBuilder.Core.Models.Styles;
using Microsoft.EntityFrameworkCore;

namespace LyricBuilder.Core.Services.Styles;

public interface IAdminStyleService
{
    Task<StatefulPagedResult<StyleModel>> GetStylesAsync(AdminStyleFilter filter, CancellationToken ct);
    Task<StatefulResult<StyleModel>> GetStyleByIdAsync(Guid id, CancellationToken ct);
    Task<MutationOperationResult> CreateStyleAsync(StyleMutation mutation, CancellationToken ct);
    Task<MutationOperationResult> UpdateStyleAsync(Guid id, StyleMutation mutation, CancellationToken ct);
    Task<MutationOperationResult> DeleteStyleAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Maintains the style catalogue. Admins only; writers read it through <see cref="StyleService"/>.
/// </summary>
/// <remarks>
/// Admins see the whole catalogue — retired styles and writing guidelines included. The admin
/// check is made here as well as on the controller, so no other caller of the service can reach
/// the catalogue by skipping the endpoint.
/// </remarks>
public sealed class AdminStyleService(
    ILogger<AdminStyleService> logger,
    IRepository<Style> styleRepository,
    IRepository<Lyric> lyricRepository,
    IUnitOfWork unitOfWork,
    RequestContext requestContext) : IAdminStyleService
{
    public async Task<StatefulPagedResult<StyleModel>> GetStylesAsync(AdminStyleFilter filter, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return StatefulPagedResult<StyleModel>.Failed(AdminOnly());

            var query = styleRepository.Query();

            if (filter.IsActive is not null)
                query = query.Where(s => s.IsActive == filter.IsActive);

            if (filter.ParentStyleId is not null)
                query = query.Where(s => s.ParentStyleId == filter.ParentStyleId);

            if (filter.Name.IsNotNullOrEmpty())
                query = query.Where(s => s.Name.Contains(filter.Name));

            var total = await query.LongCountAsync(ct);
            var styles = await query
                .OrderBy(s => s.Name)
                .Skip(filter.Skip)
                .Take(filter.PageSize)
                .ToListAsync(ct);

            var models = styles.Select(s => StyleModel.Map(s, includeGuidelines: true)).ToList();
            return StatefulPagedResult<StyleModel>.Success(models, total);
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting styles for admin");
            return StatefulPagedResult<StyleModel>.Failed(
                InternalError.InternalServerError("error while getting styles"));
        }
    }

    public async Task<StatefulResult<StyleModel>> GetStyleByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return StatefulResult<StyleModel>.Failed(AdminOnly());

            var style = await styleRepository.GetByIdAsync(id, ct);
            if (style is null)
                return StatefulResult<StyleModel>.Failed(StyleNotFound());

            return StatefulResult<StyleModel>.Success(StyleModel.Map(style, includeGuidelines: true));
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting style {StyleId} for admin", id);
            return StatefulResult<StyleModel>.Failed(
                InternalError.InternalServerError("error while getting style"));
        }
    }

    public async Task<MutationOperationResult> CreateStyleAsync(StyleMutation mutation, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return MutationOperationResult.Failed(AdminOnly());

            if (await styleRepository.AnyAsync(s => s.Slug == mutation.Slug, ct))
                return MutationOperationResult.Failed(SlugTaken(mutation.Slug));

            var style = new Style { Slug = mutation.Slug };

            var parentError = await ValidateParentAsync(style.Id, mutation.ParentStyleId, ct);
            if (parentError is not null)
                return MutationOperationResult.Failed(parentError);

            Apply(style, mutation);

            await styleRepository.AddAsync(style, ct);
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(style.Id, "style created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while creating style");
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while creating style"));
        }
    }

    public async Task<MutationOperationResult> UpdateStyleAsync(Guid id, StyleMutation mutation, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return MutationOperationResult.Failed(AdminOnly());

            var style = await styleRepository.GetByIdAsync(id, ct);
            if (style is null)
                return MutationOperationResult.Failed(StyleNotFound());

            // Prompts and clients refer to styles by slug, so renaming one would break them.
            if (mutation.Slug != style.Slug)
                return MutationOperationResult.Failed(InternalError.BadRequest(
                    $"slug of style {id} cannot change from '{style.Slug}' to '{mutation.Slug}'",
                    "a style's slug cannot be changed"));

            var parentError = await ValidateParentAsync(style.Id, mutation.ParentStyleId, ct);
            if (parentError is not null)
                return MutationOperationResult.Failed(parentError);

            Apply(style, mutation);

            styleRepository.Update(style);
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(style.Id, "style updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while updating style {StyleId}", id);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while updating style"));
        }
    }

    public async Task<MutationOperationResult> DeleteStyleAsync(Guid id, CancellationToken ct)
    {
        try
        {
            if (!requestContext.IsAdmin)
                return MutationOperationResult.Failed(AdminOnly());

            var style = await styleRepository.GetByIdAsync(id, ct);
            if (style is null)
                return MutationOperationResult.Failed(StyleNotFound());

            // A deleted style would vanish from under its lyrics and orphan its sub-styles.
            // Retiring it (IsActive = false) is the way to take a style in use out of circulation.
            if (await lyricRepository.AnyAsync(l => l.StyleId == id, ct))
                return MutationOperationResult.Failed(InternalError.Conflict(
                    $"style {id} is used by lyrics",
                    "style is used by lyrics; deactivate it instead"));

            if (await styleRepository.AnyAsync(s => s.ParentStyleId == id, ct))
                return MutationOperationResult.Failed(InternalError.Conflict(
                    $"style {id} has sub-styles",
                    "style has sub-styles; move or delete them first"));

            styleRepository.Remove(style);
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(id, "style deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while deleting style {StyleId}", id);
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while deleting style"));
        }
    }

    /// <summary>
    /// The parent must exist and must not be the style itself or any of its descendants, or the
    /// fallback from sub-style to parent would loop.
    /// </summary>
    private async Task<InternalError?> ValidateParentAsync(Guid styleId, Guid? parentStyleId, CancellationToken ct)
    {
        var ancestorId = parentStyleId;
        while (ancestorId is { } currentId)
        {
            if (currentId == styleId)
                return InternalError.BadRequest(
                    $"style {styleId} cannot be nested under itself or its own sub-style",
                    "a style cannot be nested under itself or its own sub-style");

            var ancestor = await styleRepository.GetByIdAsync(currentId, ct);
            if (ancestor is null)
                return InternalError.BadRequest(
                    $"parent style {currentId} not found", "parent style not found");

            ancestorId = ancestor.ParentStyleId;
        }

        return null;
    }

    private static void Apply(Style style, StyleMutation mutation)
    {
        style.Name = mutation.Name.Trim();
        style.Description = mutation.Description;
        style.WritingGuidelines = mutation.WritingGuidelines;
        style.ParentStyleId = mutation.ParentStyleId;
        style.IsActive = mutation.IsActive;
    }

    private static InternalError StyleNotFound() => InternalError.NotFound("style not found");

    private static InternalError SlugTaken(string slug) =>
        InternalError.Conflict($"style slug '{slug}' is taken", "a style with this slug already exists");

    private static InternalError AdminOnly() =>
        InternalError.Forbidden("the style catalogue is maintained by admins only");
}
