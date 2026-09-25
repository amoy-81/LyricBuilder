using LyricBuilder.Core;
using LyricBuilder.Core.Models.Accounts;
using LyricBuilder.Core.Services.Accounts;
using Microsoft.AspNetCore.Mvc;

namespace LyricBuilder.Host.Controllers;

/// <summary>
/// Sign-up and sign-in.
/// </summary>
[Route("api/auth")]
public class AuthController(
    ILogger<AuthController> logger,
    RequestContext requestContext,
    IAccountService accountService) : PublicEndpoint(logger, requestContext)
{
    /// <summary>Creates a writer account. Returns the new user's id; log in to get a token.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterMutation mutation)
    {
        var result = await accountService.RegisterAsync(mutation, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Exchanges a username and password for a bearer access token.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginMutation mutation)
    {
        var result = await accountService.LoginAsync(mutation, RequestCancellationToken);
        return CreateResponse(result);
    }
}
