namespace BooksWebApp.Models;

public class PagingListAsync<T>
{
    public IQueryable<T> Data { get; } // Keep as IQueryable for compatibility
    public int TotalItems { get; }
    public int TotalPages { get; }
    public int Page { get; }
    public int Size { get; }
    public bool IsFirst { get; }
    public bool IsLast { get; }
    public bool IsNext { get; }
    public bool IsPrevious { get; }

    public PagingListAsync(IQueryable<T> data, int totalItems, int page, int size)
    {
        TotalItems = totalItems;
        Page = page;
        Size = size;
        TotalPages = CalcTotalPages(totalItems, size);
        IsFirst = Page <= 1;
        IsLast = Page >= TotalPages;
        IsNext = !IsLast;
        IsPrevious = !IsFirst;
        Data = data;
    }

    public static async Task<PagingListAsync<T>> CreateAsync(
        Func<int, int, Task<IQueryable<T>>> dataGenerator,
        int totalItems,
        int page,
        int size)
    {
        var clippedPage = ClipPage(page, totalItems, size);
    
        // Ensure only the requested page data is fetched
        var data = await dataGenerator(clippedPage, size);
    
        // Apply Skip and Take correctly
        data = data
            .Skip((clippedPage - 1) * size) // Skip items for previous pages
            .Take(size); // Limit to the requested page size

        return new PagingListAsync<T>(data, totalItems, clippedPage, size);
    }


    private static int CalcTotalPages(int totalItems, int size)
    {
        return (int)Math.Ceiling(totalItems / (double)size);
    }

    private static int ClipPage(int page, int totalItems, int size)
    {
        int totalPages = CalcTotalPages(totalItems, size);
        if (page > totalPages)
        {
            return totalPages;
        }
        return page < 1 ? 1 : page;
    }
}