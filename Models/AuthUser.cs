namespace FrontendNet.Models;

/// <summary>Authentication response returned by POST /api/auth and POST /api/auth/refresh.</summary>
public class AuthUser
{
    public string Id { get; set; } = string.Empty;
    public required string Email { get; set; }
    public required string Name { get; set; }
    public required string Profile { get; set; }
    public string? Status { get; set; }
    public string? SellerStatus { get; set; }
    public required string Jwt { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string? TokenType { get; set; }
}
