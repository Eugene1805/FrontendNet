using FrontendNet.Models;
namespace FrontendNet.Services;
public class AuditLogClientService(HttpClient client)
{
    public async Task<List<AuditEntry>?> GetAsync()
    {
        return await client.GetFromJsonAsync<List<AuditEntry>>("api/audit-logs");
    }
}