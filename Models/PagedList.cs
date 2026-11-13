namespace FrontendNet.Models;

/// <summary>Paginación en el frontend (el API aún no page; ver DD-09).</summary>
public class PagedList<T>
{
    public List<T> Items { get; }
    public int Paginate { get; }
    public int Tamano { get; }
    public int Total { get; }
    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling(Total / (double)Tamano));

    public PagedList(IEnumerable<T> source, int page, int pageSize)
    {
        var items = source as IList<T> ?? source.ToList();
        Tamano = Math.Clamp(pageSize, 1, 200);
        Total = items.Count;
        Paginate = Math.Clamp(page, 1, TotalPaginas);
        Items = items.Skip((Paginate - 1) * Tamano).Take(Tamano).ToList();
    }
}
