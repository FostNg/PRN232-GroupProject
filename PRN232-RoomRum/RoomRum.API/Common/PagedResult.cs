namespace RoomRum.API.Common;

/// <summary>
/// Generic pagination envelope shared by every paged endpoint.
/// ListingPagedResponse keeps its original shape for backward compatibility
/// with the already-released client, new modules use this type.
/// </summary>
public sealed class PagedResult<T>
{
    public List<T> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalItems { get; init; }

    public int TotalPages { get; init; }

    public bool HasNextPage => Page < TotalPages;

    public bool HasPreviousPage => Page > 1;

    public static PagedResult<T> Create(
        List<T> items,
        int page,
        int pageSize,
        int totalItems)
    {
        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }
}

/// <summary>
/// Shared pagination guard rails so every endpoint validates the same way.
/// </summary>
public static class PaginationRules
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;

    /// <summary>
    /// Throws ArgumentException with a client friendly message when the values
    /// are out of range, instead of silently clamping them.
    /// </summary>
    public static void Validate(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new ArgumentException(
                "Page must be greater than or equal to 1.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new ArgumentException(
                $"PageSize must be between 1 and {MaxPageSize}.");
        }
    }
}
