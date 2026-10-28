namespace RoomRum.API;

/// <summary>
/// Uniform response envelope used by every endpoint:
/// { "success": bool, "message": string?, "data": T? }.
/// Keeping one shape means the Flutter client only needs a single
/// ApiResponse&lt;T&gt; model and one error-handling path.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    public T? Data { get; init; }
}

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ApiResponse<T> Fail<T>(string message)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message
        };
    }

    /// <summary>
    /// Non-generic failure used by middleware, where no payload type is known.
    /// </summary>
    public static ApiResponse<object> Fail(string message)
    {
        return new ApiResponse<object>
        {
            Success = false,
            Message = message
        };
    }
}
