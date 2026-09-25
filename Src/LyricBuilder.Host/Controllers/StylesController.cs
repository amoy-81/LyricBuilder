using LyricBuilder.Core;
using LyricBuilder.Core.Models.Styles;
using LyricBuilder.Core.Services.Styles;
using Microsoft.AspNetCore.Mvc;

namespace LyricBuilder.Host.Controllers;

/// <summary>
/// The style catalogue, for writers choosing a style. Read-only; admins maintain it through
/// <see cref="AdminStylesController"/>.
/// </summary>
[Route("api/styles")]
public class StylesController(
    ILogger<StylesController> logger,
    RequestContext requestContext,
    IStyleService styleService) : SecureEndpoint(logger, requestContext)
{
    /// <summary>Active styles matching the filter, by name.</summary>
    [HttpGet]
    public async Task<IActionResult> GetStyles([FromQuery] StyleFilter filter)
    {
        var result = await styleService.GetStylesAsync(filter, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>One style by id, including a retired one.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStyleById([FromRoute] Guid id)
    {
        var result = await styleService.GetStyleByIdAsync(id, RequestCancellationToken);
        return CreateResponse(result);
    }
}
