using FrontendNet.Models;
namespace FrontendNet.Services;
public class RolesClientService(HttpClient client)
{
    public async Task<List<Role>?> GetAsync()
    {
        return await client.GetFromJsonAsync<List<Role>>("api/roles");
    }
}