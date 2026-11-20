namespace FrontendNet.Models;

/// <summary>Canonical user profile roles defined by the backend API.</summary>
public static class Roles
{
    public const string Admin = "ADMIN";
    public const string Seller = "SELLER";
    public const string Buyer = "BUYER";
}

/// <summary>Account statuses defined by the backend API.</summary>
public static class UserStatuses
{
    public const string Active = "ACTIVE";
    public const string Suspended = "SUSPENDED";
    public const string Disabled = "DISABLED";

    public static readonly string[] All = [Active, Suspended, Disabled];
}

/// <summary>Seller review lifecycle statuses defined by the backend API.</summary>
public static class SellerStatuses
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Suspended = "SUSPENDED";
}
