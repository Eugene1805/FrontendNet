namespace FrontendNet.Models;

/// <summary>Frontend pagination slice over an in-memory collection.</summary>
public class PagedList<T>
{
    public List<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int Total { get; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public PagedList(IEnumerable<T> source, int page, int pageSize)
    {
        var items = source as IList<T> ?? source.ToList();
        PageSize = Math.Clamp(pageSize, 1, 200);
        Total = items.Count;
        Page = Math.Clamp(page, 1, TotalPages);
        Items = items.Skip((Page - 1) * PageSize).Take(PageSize).ToList();
    }
}
