using System.Net.Http.Headers;
using FrontendNet.Models;
namespace FrontendNet.Services;

public class FilesClientService(HttpClient client)
{
    public async Task<List<FileRecord>?> GetAsync()
    {
        return await client.GetFromJsonAsync<List<FileRecord>>("api/files");
    }
    /// <summary>File metadata. GET api/files/{id} returns the binary; JSON is available at /details.</summary>
    public async Task<FileRecord?> GetAsync(int id)
    {
        return await client.GetFromJsonAsync<FileRecord>($"api/files/{id}/details");
    }
    /// <summary>Binario de la imagen (para el proxy de imágenes, DD-05). El endpoint es público en el API.</summary>
    public async Task<(byte[] Contenido, string Mime)?> GetBinarioAsync(int id)
    {
        try
        {
            using var response = await client.GetAsync($"api/files/{id}");
            var mime = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return (await response.Content.ReadAsByteArrayAsync(), mime);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    public async Task PostAsync(Upload fileRecord)
    {
        using var form = await CreateFormAsync(fileRecord);
        using var response = await client.PostAsync("api/files", form);
    }
    public async Task PutAsync(Upload fileRecord)
    {
        using var form = await CreateFormAsync(fileRecord);
        using var response = await client.PutAsync($"api/files/{fileRecord.FileId}", form);
    }
    public async Task DeleteAsync(int id)
    {
        using var response = await client.DeleteAsync($"api/files/{id}");
    }

    private static async Task<MultipartFormDataContent> CreateFormAsync(Upload fileRecord)
    {
        using var memoryStream = new MemoryStream();
        await fileRecord.Cover.CopyToAsync(memoryStream);
        var fileContent = new ByteArrayContent(memoryStream.ToArray());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(fileRecord.Cover.ContentType);
        return new MultipartFormDataContent { { fileContent, "file", fileRecord.Cover.FileName! } };
    }
}
