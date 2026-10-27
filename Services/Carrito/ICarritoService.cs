using FrontendNet.Models;
namespace FrontendNet.Services;

/// <summary>
/// Contrato del carrito (docs/planeacion_frontend.md 5.1). Las vistas y controladores solo dependen de esta interfaz:
/// hoy la implementa <see cref="CarritoSesionService"/> (simulación) y, cuando el API exista, <see cref="CarritoClientService"/>.
/// </summary>
public interface ICarritoService
{
    Task<Carrito> GetAsync();
    /// <summary>Total de artículos (para el menú); debe ser barato porque se pide en cada página.</summary>
    Task<int> ContarAsync();
    Task<Carrito> AgregarAsync(int productoId, int cantidad);
    Task<Carrito> ActualizarAsync(int productoId, int cantidad);
    Task QuitarAsync(int productoId);
    Task VaciarAsync();
}
