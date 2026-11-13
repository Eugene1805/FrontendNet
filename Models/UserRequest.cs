namespace FrontendNet.Models;
using System.Text.Json.Serialization;

/// <summary>Write body for PUT api/users/:email. Password is optional.</summary>
public class UserRequest
{
    public required string Name { get; set; }
    public string? Profile { get; set; }
    public string? Status { get; set; }
    [JsonIgnore]
    public string? Role { get => Profile; set => Profile = value; }
    public string? Password { get; set; }
}
