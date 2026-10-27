namespace FrontendNet.Models;

/// <summary>Cuerpo de error del API: { status?, mensaje, errores?: [{ type, msg, path, location, value }] }.</summary>
public class ApiError
{
    public int? Status { get; set; }
    public string? Mensaje { get; set; }
    public List<ApiErrorCampo>? Errores { get; set; }
}

public class ApiErrorCampo
{
    public string? Msg { get; set; }
    public string? Path { get; set; }
}
