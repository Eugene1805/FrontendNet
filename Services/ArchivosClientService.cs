using System.Net.Http.Headers;
using FrontendNet.Models;
namespace FrontendNet.Services;

public class ArchivosClientService(HttpClient client)
{
    public async Task<List<Archivo>?> GetAsync()
    {
        return await client.GetFromJsonAsync<List<Archivo>>("api/archivos");
    }
    /// <summary>Metadatos del archivo. GET api/archivos/{id} devuelve el binario; el JSON está en /detalle (D-02).</summary>
    public async Task<Archivo?> GetAsync(int id)
    {
        return await client.GetFromJsonAsync<Archivo>($"api/archivos/{id}/detalle");
    }
    /// <summary>Binario de la imagen (para el proxy de imágenes, DD-05). El endpoint es público en el API.</summary>
    public async Task<(byte[] Contenido, string Mime)?> GetBinarioAsync(int id)
    {
        try
        {
            using var response = await client.GetAsync($"api/archivos/{id}");
            var mime = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return (await response.Content.ReadAsByteArrayAsync(), mime);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    public async Task PostAsync(Upload Archivo)
    {
        using var form = await CreaFormularioAsync(Archivo);
        using var response = await client.PostAsync("api/archivos", form);
    }
    public async Task PutAsync(Upload Archivo)
    {
        using var form = await CreaFormularioAsync(Archivo);
        using var response = await client.PutAsync($"api/archivos/{Archivo.ArchivoId}", form);
    }
    public async Task DeleteAsync(int id)
    {
        using var response = await client.DeleteAsync($"api/archivos/{id}");
    }

    private static async Task<MultipartFormDataContent> CreaFormularioAsync(Upload archivo)
    {
        using var memoryStream = new MemoryStream();
        await archivo.Portada.CopyToAsync(memoryStream);
        var fileContent = new ByteArrayContent(memoryStream.ToArray());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(archivo.Portada.ContentType);
        return new MultipartFormDataContent { { fileContent, "file", archivo.Portada.FileName! } };
    }
}
