using RoomRum.BLL.Services;

namespace RoomRum.API;

/// <summary>
/// Turns service failures into the uniform HTTP response envelope.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(
        HttpContext context,
        Exception exception)
    {
        // The response may already be streaming; nothing sensible left to do.
        if (context.Response.HasStarted)
        {
            _logger.LogError(
                exception,
                "Response already started; cannot write error payload.");

            throw exception;
        }

        var outcome = ExceptionOutcomeMapper.Map(exception);
        var statusCode = outcome.StatusCode;
        var message = outcome.Message ?? "An unexpected server error occurred.";

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}.",
                context.Request.Method,
                context.Request.Path);
        }
        else
        {
            // Keep the original exception for diagnostics while returning a safe message.
            _logger.LogWarning(
                exception,
                "Business rule rejected {Method} {Path}: {Message}",
                context.Request.Method,
                context.Request.Path,
                message);
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsJsonAsync(
            ApiResponse.Fail(message),
            context.RequestAborted);
    }

}
