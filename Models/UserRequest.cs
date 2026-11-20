namespace FrontendNet.Models;

/// <summary>Strict request payload for PUT /api/users/:email (null properties are omitted).</summary>
public class UserUpdateRequest
{
    public string? Name { get; set; }
    public string? Status { get; set; }
    public string? Password { get; set; }
}

/// <summary>Response returned by seller review and suspend endpoints.</summary>
public class SellerReviewResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
}
