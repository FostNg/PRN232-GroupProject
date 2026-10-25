namespace RoomRum.BLL.DTOs.Responses;

public sealed record ApiOutcome(int StatusCode, object? Data = null, string? Message = null)
{
    public static ApiOutcome Ok(object data) => new(200, data);
    public static ApiOutcome Created(object data) => new(201, data);
    public static ApiOutcome NoContent() => new(204);
    public static ApiOutcome BadRequest(string message) => new(400, Message: message);
    public static ApiOutcome Unauthorized(string message) => new(401, Message: message);
    public static ApiOutcome Forbidden() => new(403);
    public static ApiOutcome NotFound(string? message = null) => new(404, Message: message);
    public static ApiOutcome Conflict(string message) => new(409, Message: message);
    public static ApiOutcome Unavailable(string message) => new(503, Message: message);
}
