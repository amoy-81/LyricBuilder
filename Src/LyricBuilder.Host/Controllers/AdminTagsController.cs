using LyricBuilder.Core;
using LyricBuilder.Core.Models.Tags;
using LyricBuilder.Core.Services.Tags;
using Microsoft.AspNetCore.Mvc;

namespace LyricBuilder.Host.Controllers;

/// <summary>
/// Maintains the mood and theme tags, inactive ones included. Admins only.
/// </summary>
[Route("api/admin/tags")]
public class AdminTagsController(
    ILogger<AdminTagsController> logger,
    RequestContext requestContext,
    IAdminTagService adminTagService) : AdminEndpoint(logger, requestContext)
{
    /// <summary>Tags matching the filter, active and inactive, by category and name.</summary>
    [HttpGet]
    public async Task<IActionResult> GetTags([FromQuery] AdminTagFilter filter)
    {
        var result = await adminTagService.GetTagsAsync(filter, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>One tag by id, including an inactive one.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTagById([FromRoute] Guid id)
    {
        var result = await adminTagService.GetTagByIdAsync(id, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Adds a tag.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateTag([FromBody] TagMutation mutation)
    {
        var result = await adminTagService.CreateTagAsync(mutation, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Updates a tag. The slug cannot change; clear IsActive to hide it from writers.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTag([FromRoute] Guid id, [FromBody] TagMutation mutation)
    {
        var result = await adminTagService.UpdateTagAsync(id, mutation, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Soft-deletes a tag; lyrics that carry it lose it.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTag([FromRoute] Guid id)
    {
        var result = await adminTagService.DeleteTagAsync(id, RequestCancellationToken);
        return CreateResponse(result);
    }
}
