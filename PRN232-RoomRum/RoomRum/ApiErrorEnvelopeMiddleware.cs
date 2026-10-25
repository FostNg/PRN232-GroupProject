using System.Text.Json;

using RoomRum.API;

namespace RoomRum.API;

/// <summary>
/// Forces every error response into the same ApiResponse envelope.
///
/// ExceptionHandlingMiddleware covers exceptions, but ASP.NET Core produces
/// several error responses on its own and they do NOT share a shape:
///   - 401 from the JWT handler: empty body;
///   - 403 from [Authorize]: empty body;
///   - 404 for an unknown route: empty body;
///   - 405 / 415: empty body with an Allow header;
///   - 400 model validation and malformed JSON: RFC 7807 problem+json
///     ({"type","title","status","errors","traceId"}).
///
/// A Flutter client that decodes one ApiResponse&lt;T&gt; model would crash or show a
/// blank message on the empty and problem+json variants - and model validation
/// is exactly where field-level messages matter. This middleware rewrites all of
/// them into { success, message, data } and leaves responses that already carry
/// the envelope untouched.
/// </summary>
public sealed class ApiErrorEnvelopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiErrorEnvelopeMiddleware> _logger;

    public ApiErrorEnvelopeMiddleware(
        RequestDelegate next,
        ILogger<ApiErrorEnvelopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var originalBody = context.Response.Body;

        await using var capture = new StatusAwareCaptureStream(context, originalBody);
        context.Response.Body = capture;

        try
        {
            await _next(context);

            if (context.Response.StatusCode < StatusCodes.Status400BadRequest)
                return;

            if (capture.ForwardedSuccess)
            {
                _logger.LogWarning("Response status changed to {StatusCode} after success body streaming began.",
                    context.Response.StatusCode);
                return;
            }

            var captured = capture.ErrorTooLarge ? string.Empty : capture.CapturedErrorBody;

            if (IsEnvelope(captured))
            {
                // Already { success, ... } - the exception middleware or a
                // controller wrote it. Some call sites build the body by hand
                // and omit `data`; add it so the client always finds all three
                // keys and never has to null-check the property's existence.
                await WriteRawAsync(
                    context,
                    originalBody,
                    EnsureDataKey(captured),
                    "application/json; charset=utf-8");
                return;
            }

            var message = BuildMessage(context.Response.StatusCode, captured);

            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers.ContentLength = null;

            var payload = JsonSerializer.Serialize(
                ApiResponse.Fail(message),
                JsonOptions);

            await WriteRawAsync(
                context,
                originalBody,
                payload,
                "application/json; charset=utf-8");
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Success responses stream directly to the client. Only error bodies are
    // captured, with a hard cap so a malformed upstream error cannot grow memory
    // without bound. The middleware replaces the captured error after _next ends.
    private sealed class StatusAwareCaptureStream(HttpContext context, Stream destination) : Stream
    {
        private const int MaxErrorBytes = 64 * 1024;
        private readonly MemoryStream _error = new();
        private bool _forwardedSuccess;
        private bool _errorTooLarge;

        public bool ForwardedSuccess => _forwardedSuccess;
        public bool ErrorTooLarge => _errorTooLarge;
        public string CapturedErrorBody => System.Text.Encoding.UTF8.GetString(_error.ToArray());
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            if (_forwardedSuccess) destination.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            _forwardedSuccess ? destination.FlushAsync(cancellationToken) : Task.CompletedTask;

        public override void Write(byte[] buffer, int offset, int count) =>
            Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (context.Response.StatusCode < StatusCodes.Status400BadRequest || _forwardedSuccess)
            {
                _forwardedSuccess = true;
                destination.Write(buffer);
            }
            else
            {
                Capture(buffer);
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (context.Response.StatusCode < StatusCodes.Status400BadRequest || _forwardedSuccess)
            {
                _forwardedSuccess = true;
                return destination.WriteAsync(buffer, cancellationToken);
            }
            Capture(buffer.Span);
            return ValueTask.CompletedTask;
        }

        private void Capture(ReadOnlySpan<byte> bytes)
        {
            if (_errorTooLarge) return;
            if (bytes.Length > MaxErrorBytes - _error.Length)
            {
                _errorTooLarge = true;
                _error.SetLength(0);
                return;
            }
            _error.Write(bytes);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask DisposeAsync()
        {
            _error.Dispose();
            return ValueTask.CompletedTask;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) _error.Dispose();
            base.Dispose(disposing);
        }
    }

    private static async Task WriteRawAsync(
        HttpContext context,
        Stream destination,
        string body,
        string contentType)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);

        context.Response.ContentType = contentType;
        context.Response.ContentLength = bytes.Length;

        await destination.WriteAsync(bytes, context.RequestAborted);
    }

    /// <summary>
    /// Adds "data": null when a hand-built body carries success/message but no
    /// data key. The JSON text is reused verbatim when the key is present, so
    /// no payload detail is lost.
    /// </summary>
    private static string EnsureDataKey(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                document.RootElement.TryGetProperty("data", out _))
            {
                return body;
            }
        }
        catch (JsonException)
        {
            return body;
        }

        // Insert the missing key just before the closing brace, keeping the
        // original properties untouched.
        var close = body.LastIndexOf('}');

        if (close < 0)
        {
            return body;
        }

        var head = body[..close].TrimEnd();
        var separator = head.EndsWith("{", StringComparison.Ordinal) ? "" : ",";

        return head + separator + "\"data\":null" + body[close..];
    }

    /// <summary>
    /// True when the captured body is already the uniform envelope, so the
    /// original payload (with any field-level detail) is passed through.
    /// </summary>
    private static bool IsEnvelope(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("success", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Produces a human-readable message for a framework-generated error,
    /// keeping the field-level validation detail when there is any.
    /// </summary>
    private static string BuildMessage(int statusCode, string body)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(root.GetString()))
                    return root.GetString()!;

                if (root.ValueKind != JsonValueKind.Object)
                    return DefaultMessage(statusCode);

                // RFC 7807 model validation: {"title":..., "errors":{field:[...]}}
                if (root.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind == JsonValueKind.Object)
                {
                    var parts = new List<string>();

                    foreach (var field in errors.EnumerateObject())
                    {
                        if (field.Value.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var entry in field.Value.EnumerateArray())
                        {
                            var text = entry.GetString();

                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                parts.Add(text);
                            }
                        }
                    }

                    if (parts.Count > 0)
                    {
                        return string.Join(" ", parts);
                    }
                }

                if (root.TryGetProperty("detail", out var detail) &&
                    detail.ValueKind == JsonValueKind.String)
                {
                    var text = detail.GetString();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }

                if (root.TryGetProperty("title", out var title) &&
                    title.ValueKind == JsonValueKind.String)
                {
                    var text = title.GetString();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through to the status-code default below.
            }
        }

        return DefaultMessage(statusCode);
    }

    private static string DefaultMessage(int statusCode) => statusCode switch
        {
            StatusCodes.Status400BadRequest =>
                "The request was rejected. Check the submitted values.",
            StatusCodes.Status401Unauthorized =>
                "Authentication is required. Sign in and try again.",
            StatusCodes.Status403Forbidden =>
                "You do not have permission to perform this action.",
            StatusCodes.Status404NotFound =>
                "The requested resource was not found.",
            StatusCodes.Status405MethodNotAllowed =>
                "This HTTP method is not allowed for the endpoint.",
            StatusCodes.Status409Conflict =>
                "The request conflicts with the current state. Reload and try again.",
            StatusCodes.Status415UnsupportedMediaType =>
                "The request body must be sent as application/json.",
            StatusCodes.Status429TooManyRequests =>
                "Too many requests. Please slow down and try again.",
            _ => "The request could not be completed."
        };
}
