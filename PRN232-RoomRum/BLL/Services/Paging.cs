namespace RoomRum.BLL.Services;

public static class Paging
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    public static bool TryGetSkip(int page, int pageSize, out int skip)
    {
        skip = 0;
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize ||
            (long)(page - 1) * pageSize > int.MaxValue)
            return false;
        skip = (page - 1) * pageSize;
        return true;
    }
}
