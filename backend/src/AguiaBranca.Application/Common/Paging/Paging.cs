using AguiaBranca.Application.Common.Results;

namespace AguiaBranca.Application.Common.Paging;

public sealed record PageRequest(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public int Skip => (Page - 1) * PageSize;

    public IReadOnlyList<Error> Validate()
    {
        var errors = new List<Error>();
        if (Page < 1) errors.Add(Error.Validation("page deve ser >= 1.", "page"));
        if (PageSize is < 1 or > MaxPageSize) errors.Add(Error.Validation($"pageSize deve estar entre 1 e {MaxPageSize}.", "pageSize"));
        return errors;
    }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) =>
        new(Items.Select(map).ToList(), Page, PageSize, TotalItems);

    public static PagedResult<T> Empty(PageRequest page) => new([], page.Page, page.PageSize, 0);
}
