using System.ComponentModel.DataAnnotations;

namespace LyricBuilder.Core.Models.Accounts;

/// <summary>Sign-in body, exchanged for an <see cref="AccessTokenModel"/>.</summary>
public class LoginMutation
{
    [Required]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
