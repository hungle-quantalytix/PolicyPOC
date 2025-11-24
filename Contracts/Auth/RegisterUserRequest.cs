using System.ComponentModel.DataAnnotations;

namespace PolicyPOC.Contracts.Auth;

public class RegisterUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
    public string? Department { get; set; }
}


