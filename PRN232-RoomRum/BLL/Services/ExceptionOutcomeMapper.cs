using RoomRum.BLL.DTOs.Responses;
using RoomRum.DAL;

namespace RoomRum.BLL.Services;

public static class ExceptionOutcomeMapper
{
    public static ApiOutcomeResponse Map(Exception exception)
    {
        if (DataAccessFailureClassifier.TryClassify(exception, out var failure))
            return failure switch
            {
                DataAccessFailure.ConcurrentModification => ApiOutcomeResponse.Conflict(
                    "The record was modified by another request. Reload it and try again."),
                DataAccessFailure.Duplicate => ApiOutcomeResponse.Conflict("A record with these values already exists."),
                DataAccessFailure.Constraint => ApiOutcomeResponse.BadRequest(
                    "The request violates a database relationship or constraint."),
                DataAccessFailure.ValueTooLong => ApiOutcomeResponse.BadRequest("A value exceeds a database column limit."),
                DataAccessFailure.Deadlock => ApiOutcomeResponse.Conflict(
                    "The request conflicted with another operation. Try again."),
                _ => new ApiOutcomeResponse { StatusCode = 500, Message = "A database error occurred." }
            };

        return exception switch
        {
            ArgumentException argument => ApiOutcomeResponse.BadRequest(argument.Message),
            KeyNotFoundException notFound => ApiOutcomeResponse.NotFound(notFound.Message),
            UnauthorizedAccessException => new ApiOutcomeResponse { StatusCode = 403, Message = exception.Message },
            InvalidOperationException invalid => ApiOutcomeResponse.Conflict(invalid.Message),
            _ => new ApiOutcomeResponse { StatusCode = 500, Message = "An unexpected server error occurred." }
        };
    }
}
