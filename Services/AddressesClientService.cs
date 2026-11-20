using FrontendNet.Helpers;
using FrontendNet.Models;

namespace FrontendNet.Services;

public class AddressesClientService(HttpClient client)
{
    public async Task<List<Address>> GetAsync() =>
        await client.GetFromJsonAsync<List<Address>>("api/addresses") ?? [];

    public async Task<Address?> CreateAsync(AddressFormModel model)
    {
        using var response = await client.PostAsJsonAsync("api/addresses", BuildPayload(model));
        return await response.Content.ReadFromJsonAsync<Address>();
    }

    public async Task UpdateAsync(string id, AddressFormModel model)
    {
        using var response = await client.PutAsJsonAsync($"api/addresses/{UrlHelper.Encode(id)}", BuildPayload(model));
    }

    public async Task DeleteAsync(string id)
    {
        using var response = await client.DeleteAsync($"api/addresses/{UrlHelper.Encode(id)}");
    }

    public async Task SetDefaultAsync(string id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/addresses/{UrlHelper.Encode(id)}/default");
        using var response = await client.SendAsync(request);
    }

    private static Dictionary<string, object?> BuildPayload(AddressFormModel model) => new()
    {
        ["label"] = string.IsNullOrWhiteSpace(model.Label) ? null : model.Label.Trim(),
        ["recipientName"] = model.RecipientName.Trim(),
        ["street"] = model.Street.Trim(),
        ["exteriorNumber"] = model.ExteriorNumber.Trim(),
        ["interiorNumber"] = string.IsNullOrWhiteSpace(model.InteriorNumber) ? null : model.InteriorNumber.Trim(),
        ["neighborhood"] = model.Neighborhood.Trim(),
        ["city"] = model.City.Trim(),
        ["state"] = model.State.Trim(),
        ["postalCode"] = model.PostalCode.Trim(),
        ["country"] = model.Country.Trim().ToUpperInvariant(),
        ["isDefault"] = model.IsDefault
    };
}
