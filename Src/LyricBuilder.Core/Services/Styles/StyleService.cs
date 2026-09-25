using LyricBuilder.Core.Models.Styles;
using Microsoft.EntityFrameworkCore;

namespace LyricBuilder.Core.Services.Styles;

public interface IStyleService
{
    Task<StatefulPagedResult<StyleModel>> GetStylesAsync(StyleFilter filter, CancellationToken ct);
    Task<StatefulResult<StyleModel>> GetStyleByIdAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// The style catalogue as writers see it: read-only, without the writing guidelines, which are
/// written for the model. Admins maintain it through <see cref="AdminStyleService"/>.
/// </summary>
public sealed class StyleService(
    ILogger<StyleService> logger,
    IRepository<Style> styleRepository) : IStyleService
{
    public async Task<StatefulPagedResult<StyleModel>> GetStylesAsync(StyleFilter filter, CancellationToken ct)
    {
        try
        {
            // Only active styles can be chosen for a new lyric, so only those are listed.
            var query = styleRepository.Query()
                .Where(s => s.IsActive);

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

            var models = styles.Select(s => StyleModel.Map(s)).ToList();
            return StatefulPagedResult<StyleModel>.Success(models, total);
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting styles");
            return StatefulPagedResult<StyleModel>.Failed(
                InternalError.InternalServerError("error while getting styles"));
        }
    }

    public async Task<StatefulResult<StyleModel>> GetStyleByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            // Inactive styles are still returned: a writer's existing lyric may be in one.
            var style = await styleRepository.GetByIdAsync(id, ct);
            if (style is null)
                return StatefulResult<StyleModel>.Failed(InternalError.NotFound("style not found"));

            return StatefulResult<StyleModel>.Success(StyleModel.Map(style));
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting style {StyleId}", id);
            return StatefulResult<StyleModel>.Failed(
                InternalError.InternalServerError("error while getting style"));
        }
    }
}
