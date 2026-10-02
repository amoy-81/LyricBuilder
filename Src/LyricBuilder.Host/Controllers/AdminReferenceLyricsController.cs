using LyricBuilder.Core;
using LyricBuilder.Core.Models.ReferenceLyrics;
using LyricBuilder.Core.Services.ReferenceLyrics;
using Microsoft.AspNetCore.Mvc;

namespace LyricBuilder.Host.Controllers;

/// <summary>
/// Maintains the reference lyrics that AI section writing takes its examples from. Admins only.
/// </summary>
[Route("api/admin/reference-lyrics")]
public class AdminReferenceLyricsController(
    ILogger<AdminReferenceLyricsController> logger,
    RequestContext requestContext,
    IAdminReferenceLyricService referenceLyricService) : AdminEndpoint(logger, requestContext)
{
    /// <summary>Reference lyrics matching the filter, newest first (list — no lyric body).</summary>
    [HttpGet]
    public async Task<IActionResult> GetReferenceLyrics([FromQuery] ReferenceLyricFilter filter)
    {
        var result = await referenceLyricService.GetReferenceLyricsAsync(filter, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>One reference lyric by id, with its full content.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetReferenceLyricById([FromRoute] Guid id)
    {
        var result = await referenceLyricService.GetReferenceLyricByIdAsync(id, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Adds a reference lyric, curated by the caller. Add its sections next.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateReferenceLyric([FromBody] ReferenceLyricMutation mutation)
    {
        var result = await referenceLyricService.CreateReferenceLyricAsync(mutation, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Updates a reference lyric. The tags sent replace its current ones.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateReferenceLyric([FromRoute] Guid id, [FromBody] ReferenceLyricMutation mutation)
    {
        var result = await referenceLyricService.UpdateReferenceLyricAsync(id, mutation, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Soft-deletes a reference lyric; its sections stop being used as examples.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteReferenceLyric([FromRoute] Guid id)
    {
        var result = await referenceLyricService.DeleteReferenceLyricAsync(id, RequestCancellationToken);
        return CreateResponse(result);
    }
}
