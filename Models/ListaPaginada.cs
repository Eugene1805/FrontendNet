namespace FrontendNet.Models;

/// <summary>Paginación en el frontend (el API aún no pagina; ver DD-09).</summary>
public class ListaPaginada<T>
{
    public List<T> Items { get; }
    public int Pagina { get; }
    public int Tamano { get; }
    public int Total { get; }
    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling(Total / (double)Tamano));

    public ListaPaginada(IEnumerable<T> origen, int pagina, int tamano)
    {
        var lista = origen as IList<T> ?? origen.ToList();
        Tamano = Math.Clamp(tamano, 1, 200);
        Total = lista.Count;
        Pagina = Math.Clamp(pagina, 1, TotalPaginas);
        Items = lista.Skip((Pagina - 1) * Tamano).Take(Tamano).ToList();
    }
}
