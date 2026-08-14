namespace FieldOps.Application.Visits.Models;

public class VisitListResult
{
    public VisitListResult(IReadOnlyList<VisitListItemDto> items, int page, int pageSize, bool hasNextPage)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        HasNextPage = hasNextPage;
    }

    // TotalCount yoktur; büyük Visit tablosunda her listede COUNT(*) yerine pageSize + 1 kullanılacaktır.
    public IReadOnlyList<VisitListItemDto> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public bool HasNextPage { get; }
}
