namespace OT.Assessment.Application.Dtos;

/// A book has 35 stories and each page fits 10. Reading page 1 gives you Data = the first 10 stories, Page = 1, PageSize = 10, Total = 35 and TotalPages = 4.
public sealed record PagedResult<T>(
    IReadOnlyList<T> Data,
    int Page,
    int PageSize,
    int Total,
    int TotalPages)
{
    /// 35 items at 10 per page is 3.5 pages, which rounds up to 4 pages. 0 items at 10 per page is 0 pages.
    public static PagedResult<T> Create(IReadOnlyList<T> data, int page, int pageSize, int total)
    {
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        return new PagedResult<T>(data, page, pageSize, total, totalPages);
    }
}
