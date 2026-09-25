using LyricBuilder.Core;
using LyricBuilder.Core.Models.Lyrics;
using LyricBuilder.Core.Services.Lyrics;
using Microsoft.AspNetCore.Mvc;

namespace LyricBuilder.Host.Controllers;

/// <summary>
/// The caller's own lyrics. Every endpoint needs a signed-in caller and sees only what they wrote.
/// </summary>
[Route("api/lyrics")]
public class LyricsController(
    ILogger<LyricsController> logger,
    RequestContext requestContext,
    ILyricService lyricService) : SecureEndpoint(logger, requestContext)
{
    /// <summary>The caller's lyrics matching the filter (list — no lyric body).</summary>
    [HttpGet]
    public async Task<IActionResult> GetLyrics([FromQuery] LyricFilter filter)
    {
        var result = await lyricService.GetLyricsAsync(filter, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>One of the caller's lyrics by id, with its full content.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetLyricById([FromRoute] Guid id)
    {
        var result = await lyricService.GetLyricByIdAsync(id, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Creates a lyric owned by the caller.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateLyric([FromBody] LyricMutation mutation)
    {
        var result = await lyricService.CreateLyricAsync(mutation, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Updates a lyric the caller owns.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateLyric([FromRoute] Guid id, [FromBody] LyricMutation mutation)
    {
        var result = await lyricService.UpdateLyricAsync(id, mutation, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Soft-deletes a lyric the caller owns.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLyric([FromRoute] Guid id)
    {
        var result = await lyricService.DeleteLyricAsync(id, RequestCancellationToken);
        return CreateResponse(result);
    }
}
