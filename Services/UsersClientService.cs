using System.Text.Json;
using System.Text.Json.Serialization;
using FrontendNet.Helpers;
using FrontendNet.Models;

namespace FrontendNet.Services;

public class UsersClientService(HttpClient client)
{
    private static readonly JsonSerializerOptions OmitNullsOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<List<User>?> GetAsync() =>
        await client.GetFromJsonAsync<List<User>>("api/users");

    public async Task<User?> GetAsync(string email) =>
        await client.GetFromJsonAsync<User>($"api/users/{UrlHelper.Encode(email)}");

    public async Task UpdateAsync(string email, UserUpdateRequest body)
    {
        using var response = await client.PutAsJsonAsync(
            $"api/users/{UrlHelper.Encode(email)}",
            body,
            OmitNullsOptions);
    }

    public async Task DeleteAsync(string email)
    {
        using var response = await client.DeleteAsync($"api/users/{UrlHelper.Encode(email)}");
    }

    public async Task<SellerReviewResponse?> ApproveSellerAsync(string userId)
    {
        using var response = await client.PutAsJsonAsync(
            $"api/users/sellers/{UrlHelper.Encode(userId)}/review",
            new { status = SellerStatuses.Approved });
        return await response.Content.ReadFromJsonAsync<SellerReviewResponse>();
    }

    public async Task<SellerReviewResponse?> RejectSellerAsync(string userId, string rejectionReason)
    {
        using var response = await client.PutAsJsonAsync(
            $"api/users/sellers/{UrlHelper.Encode(userId)}/review",
            new { status = SellerStatuses.Rejected, rejectionReason = rejectionReason.Trim() });
        return await response.Content.ReadFromJsonAsync<SellerReviewResponse>();
    }

    public async Task<SellerReviewResponse?> SuspendSellerAsync(string userId, string reason)
    {
        using var response = await client.PutAsJsonAsync(
            $"api/users/sellers/{UrlHelper.Encode(userId)}/suspend",
            new { reason = reason.Trim() });
        return await response.Content.ReadFromJsonAsync<SellerReviewResponse>();
    }
}
