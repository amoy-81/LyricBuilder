using LyricBuilder.Core;
using LyricBuilder.Core.Models.Tags;
using LyricBuilder.Core.Services.Tags;
using Microsoft.AspNetCore.Mvc;

namespace LyricBuilder.Host.Controllers;

/// <summary>
/// Mood and theme tags, for writers tagging a lyric. Read-only; admins maintain them through
/// <see cref="AdminTagsController"/>.
/// </summary>
[Route("api/tags")]
public class TagsController(
    ILogger<TagsController> logger,
    RequestContext requestContext,
    ITagService tagService) : SecureEndpoint(logger, requestContext)
{
    /// <summary>Active tags matching the filter, by category and name.</summary>
    [HttpGet]
    public async Task<IActionResult> GetTags([FromQuery] TagFilter filter)
    {
        var result = await tagService.GetTagsAsync(filter, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>One active tag by id; an inactive tag reads as not found.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTagById([FromRoute] Guid id)
    {
        var result = await tagService.GetTagByIdAsync(id, RequestCancellationToken);
        return CreateResponse(result);
    }
}
