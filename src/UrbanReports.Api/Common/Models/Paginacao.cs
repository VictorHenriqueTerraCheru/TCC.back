namespace UrbanReports.Api.Common;

public sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public static class Pagination
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public const int MaxPage = 10_000;

    public static int Skip(int page, int pageSize) => (page - 1) * pageSize;
}
