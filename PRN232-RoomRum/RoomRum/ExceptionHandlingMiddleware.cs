using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomRum.API;

namespace RoomRum.API;

/// <summary>
/// Turns API exceptions and SQL Server constraint violations into the
/// uniform response envelope with an appropriate HTTP status code.
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

        var (statusCode, message) = Map(exception);

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
            // The exception object is attached on purpose: services often wrap a
            // SqlException in a friendlier message, and the underlying trigger /
            // constraint detail is what makes a failing test case debuggable.
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

    private static (int StatusCode, string Message) Map(Exception exception)
    {
        return exception switch
        {
            // ------------------------------------------------------------
            // Explicit domain exceptions thrown by the services.
            // ------------------------------------------------------------
            ArgumentException argument =>
                (StatusCodes.Status400BadRequest, argument.Message),

            KeyNotFoundException notFound =>
                (StatusCodes.Status404NotFound, notFound.Message),

            UnauthorizedAccessException unauthorized =>
                (StatusCodes.Status403Forbidden, unauthorized.Message),

            InvalidOperationException invalid =>
                (StatusCodes.Status409Conflict, invalid.Message),

            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict,
                 "The record was modified by another request. " +
                 "Reload it and try again."),

            // ------------------------------------------------------------
            // SQL Server: business rules, constraints, duplicate keys.
            // ------------------------------------------------------------
            DbUpdateException { InnerException: SqlException sql } =>
                MapSqlException(sql),

            SqlException sql =>
                MapSqlException(sql),

            // ------------------------------------------------------------
            // Anything else is a genuine server fault.
            // ------------------------------------------------------------
            _ => (StatusCodes.Status500InternalServerError,
                  "An unexpected server error occurred.")
        };
    }

    private static (int StatusCode, string Message) MapSqlException(
        SqlException exception)
    {
        return exception.Number switch
        {
            2601 or 2627 =>
                (StatusCodes.Status409Conflict, "A record with these values already exists."),

            // CHECK / FOREIGN KEY constraint violation.
            547 =>
                (StatusCodes.Status400BadRequest, "The request violates a database relationship or constraint."),

            // Input that exceeds a VARCHAR or DECIMAL column limit.
            2628 or 8152 or 8115 =>
                (StatusCodes.Status400BadRequest, "A value exceeds a database column limit."),

            // Deadlock / timeout: the caller may safely retry.
            1205 =>
                (StatusCodes.Status409Conflict,
                 "The request conflicted with another operation. Try again."),

            _ => (StatusCodes.Status500InternalServerError,
                  "A database error occurred.")
        };
    }

}
