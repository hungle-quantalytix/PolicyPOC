namespace PolicyPOC.Contracts.Auth;

public class AuthResponse
{
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Department { get; set; }
    public string[] Roles { get; set; } = [];
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}


