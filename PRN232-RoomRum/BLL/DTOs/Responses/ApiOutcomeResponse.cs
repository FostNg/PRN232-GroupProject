namespace RoomRum.BLL.DTOs.Responses;

public class ApiOutcomeResponse
{
    public int StatusCode { get; set; }
    public object? Data { get; set; }
    public string? Message { get; set; }

    public static ApiOutcomeResponse Ok(object data) => new() { StatusCode = 200, Data = data };
    public static ApiOutcomeResponse Ok() => new() { StatusCode = 200 };
    public static ApiOutcomeResponse Created(object data) => new() { StatusCode = 201, Data = data };
    public static ApiOutcomeResponse Created() => new() { StatusCode = 201 };
    public static ApiOutcomeResponse NoContent() => new() { StatusCode = 204 };
    public static ApiOutcomeResponse BadRequest(string message) => new() { StatusCode = 400, Message = message };
    public static ApiOutcomeResponse Unauthorized(string message) => new() { StatusCode = 401, Message = message };
    public static ApiOutcomeResponse Forbidden() => new() { StatusCode = 403 };
    public static ApiOutcomeResponse NotFound(string? message = null) => new() { StatusCode = 404, Message = message };
    public static ApiOutcomeResponse Conflict(string message) => new() { StatusCode = 409, Message = message };
    public static ApiOutcomeResponse Unavailable(string message) => new() { StatusCode = 503, Message = message };
}
