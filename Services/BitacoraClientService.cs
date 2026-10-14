using FrontendNet.Models;
namespace FrontendNet.Services;
public class BitacoraClientService(HttpClient client)
{
    public async Task<List<Bitacora>?> GetAsync()
    {
        return await client.GetFromJsonAsync<List<Bitacora>>("api/bitacora");
    }
}