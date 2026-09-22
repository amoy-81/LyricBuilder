namespace LyricBuilder.Abstractions.Filters;

/// <summary>
/// Bound from the query string on list endpoints. <see cref="PageSize"/> is clamped so a
/// caller cannot ask for an unbounded page.
/// </summary>
public class WithPagination
{
    public const int MaxPageSize = 100;

    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 1,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    public int Skip => (Page - 1) * PageSize;
}
