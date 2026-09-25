using System.ComponentModel.DataAnnotations;

namespace LyricBuilder.Core.Models.Accounts;

/// <summary>
/// Sign-up body. Deliberately has no role — every registration is a
/// <see cref="AccountRole.User"/>, so a client cannot sign itself up as an admin.
/// </summary>
public class RegisterMutation
{
    /// <summary>Display name, shown as the author of the user's lyrics.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Letters, digits, <c>.</c> and <c>_</c>. Case-insensitive.</summary>
    [Required]
    [RegularExpression("^[a-zA-Z0-9._]{3,32}$",
        ErrorMessage = "username must be 3-32 letters, digits, '.' or '_'")]
    public string Username { get; init; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}
