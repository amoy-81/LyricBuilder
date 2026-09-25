using LyricBuilder.Core;
using LyricBuilder.Core.Models.Styles;
using LyricBuilder.Core.Services.Styles;
using Microsoft.AspNetCore.Mvc;

namespace LyricBuilder.Host.Controllers;

/// <summary>
/// Maintains the style catalogue: retired styles and writing guidelines included. Admins only.
/// </summary>
[Route("api/admin/styles")]
public class AdminStylesController(
    ILogger<AdminStylesController> logger,
    RequestContext requestContext,
    IAdminStyleService adminStyleService) : AdminEndpoint(logger, requestContext)
{
    /// <summary>Styles matching the filter, active and retired, by name.</summary>
    [HttpGet]
    public async Task<IActionResult> GetStyles([FromQuery] AdminStyleFilter filter)
    {
        var result = await adminStyleService.GetStylesAsync(filter, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>One style by id, with its writing guidelines.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStyleById([FromRoute] Guid id)
    {
        var result = await adminStyleService.GetStyleByIdAsync(id, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Adds a style to the catalogue.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateStyle([FromBody] StyleMutation mutation)
    {
        var result = await adminStyleService.CreateStyleAsync(mutation, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Updates a style. The slug cannot change.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStyle([FromRoute] Guid id, [FromBody] StyleMutation mutation)
    {
        var result = await adminStyleService.UpdateStyleAsync(id, mutation, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>
    /// Soft-deletes a style that no lyric uses and that has no sub-styles; deactivate a style in
    /// use instead.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteStyle([FromRoute] Guid id)
    {
        var result = await adminStyleService.DeleteStyleAsync(id, RequestCancellationToken);
        return CreateResponse(result);
    }
}
