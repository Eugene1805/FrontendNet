namespace FrontendNet.Models;

/// <summary>Cuerpo de PUT api/usuarios/:email. "password" es opcional.</summary>
public class UsuarioRequest
{
    public required string Nombre { get; set; }
    public required string Rol { get; set; }
    public string? Password { get; set; }
}
