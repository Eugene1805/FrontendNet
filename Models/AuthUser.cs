namespace FrontendNet.Models;
public class AuthUser{

public required string Email { get; set; }
public required string Name { get; set; }
public required string Role { get; set; }
public required string Jwt { get; set; }
public string? RefreshToken { get; set; }
public int ExpiresIn { get; set; }
public string? TokenType { get; set; }
public string? Profile { get; set; }
public string? Status { get; set; }
public string? SellerStatus { get; set; }

}