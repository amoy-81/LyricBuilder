using LyricBuilder.Core;
using LyricBuilder.Core.Models.LyricSections;
using LyricBuilder.Core.Services.LyricSections;
using Microsoft.AspNetCore.Mvc;

namespace LyricBuilder.Host.Controllers;

/// <summary>
/// The sections of one of the caller's lyrics — verses, choruses, bridges — in order.
/// </summary>
[Route("api/lyrics/{lyricId:guid}/sections")]
public class LyricSectionsController(
    ILogger<LyricSectionsController> logger,
    RequestContext requestContext,
    ILyricSectionService sectionService) : SecureEndpoint(logger, requestContext)
{
    /// <summary>The lyric's sections in performance order.</summary>
    [HttpGet]
    public async Task<IActionResult> GetSections([FromRoute] Guid lyricId)
    {
        var result = await sectionService.GetSectionsAsync(lyricId, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Adds a section, at the end unless a position is given.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSection([FromRoute] Guid lyricId, [FromBody] LyricSectionMutation mutation)
    {
        var result = await sectionService.CreateSectionAsync(lyricId, mutation, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Updates a section, and moves it when a new position is given.</summary>
    [HttpPut("{sectionId:guid}")]
    public async Task<IActionResult> UpdateSection(
        [FromRoute] Guid lyricId,
        [FromRoute] Guid sectionId,
        [FromBody] LyricSectionMutation mutation)
    {
        var result = await sectionService.UpdateSectionAsync(lyricId, sectionId, mutation, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Deletes a section and its repeats; the sections after it move up.</summary>
    [HttpDelete("{sectionId:guid}")]
    public async Task<IActionResult> DeleteSection([FromRoute] Guid lyricId, [FromRoute] Guid sectionId)
    {
        var result = await sectionService.DeleteSectionAsync(lyricId, sectionId, RequestCancellationToken);
        return CreateResponse(result);
    }
}
