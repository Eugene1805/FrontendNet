using FrontendNet.Models;

namespace FrontendNet.Services;

public class AuditLogClientService(HttpClient client)
{
    public async Task<List<AuditEntry>?> GetAsync(int limit = 50, int? before = null)
    {
        var clampedLimit = Math.Clamp(limit, 1, 100);
        var url = before is > 0
            ? $"api/audit-logs?limit={clampedLimit}&before={before.Value}"
            : $"api/audit-logs?limit={clampedLimit}";

        return await client.GetFromJsonAsync<List<AuditEntry>>(url);
    }
}
