using Microsoft.EntityFrameworkCore;
using StudiesFinal.Web.Interface;

namespace StudiesFinal.Web.Paginations
{
    public class PaginatedList<T> : List<T>, IPaginatedList
    {
        public int PageIndex { get; private set; }
        public int TotalPages { get; private set; }
        public int PageSize { get; private set; }
        public int TotalCount { get; private set; }
        public RouteValueDictionary RouteValues { get; set; }

        public PaginatedList(IEnumerable<T> items, int count, int pageIndex, int pageSize, RouteValueDictionary? routeValues = null)
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
            TotalCount = count;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);
            RouteValues = routeValues ?? new RouteValueDictionary();

            // pageNumber no se guarda aquí porque se pasa por separado en los links
            RouteValues["pageSize"] = pageSize.ToString();

            AddRange(items);
        }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize, RouteValueDictionary? routeValues = null)
        {
            if (pageSize <= 0) pageSize = int.MaxValue; // "Todos"
            if (pageIndex < 1) pageIndex = 1;

            var count = await source.CountAsync();
            var items = await source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PaginatedList<T>(items, count, pageIndex, pageSize, routeValues);
        }
    }
}
