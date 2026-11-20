using FrontendNet.Models;

namespace FrontendNet.Services;

public class ProfileClientService(HttpClient client)
{
    public async Task<UserProfile?> GetMeAsync() =>
        await client.GetFromJsonAsync<UserProfile>("api/users/me");

    public async Task<string?> GetRemainingTimeAsync()
    {
        var time = await client.GetFromJsonAsync<SessionTime>("api/auth/time");
        return time?.RemainingTime;
    }

    public async Task<UserProfile?> UpdateMeAsync(
        string name,
        string? phoneNumber,
        string? storeName,
        bool isSeller)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name.Trim(),
            ["phoneNumber"] = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim()
        };

        if (isSeller && !string.IsNullOrWhiteSpace(storeName))
            payload["storeName"] = storeName.Trim();

        using var response = await client.PutAsJsonAsync("api/users/me", payload);
        return await response.Content.ReadFromJsonAsync<UserProfile>();
    }

    public async Task ChangePasswordAsync(ChangePasswordModel model)
    {
        using var response = await client.PutAsJsonAsync(
            "api/users/me/password",
            new { currentPassword = model.CurrentPassword, newPassword = model.NewPassword });
    }

    public async Task LogoutAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
        using var response = await client.SendAsync(request);
    }
}
