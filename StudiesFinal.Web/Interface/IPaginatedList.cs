namespace StudiesFinal.Web.Interface
{
    public interface IPaginatedList
    {
        int PageIndex { get; }
        int PageSize { get; }
        int TotalPages { get; }
        int TotalCount { get; }
        bool HasPreviousPage { get; }
        bool HasNextPage { get; }
        RouteValueDictionary RouteValues { get; }
    }
}
