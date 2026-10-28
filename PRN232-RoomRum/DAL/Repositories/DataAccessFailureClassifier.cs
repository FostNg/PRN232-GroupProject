using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.DAL;

public enum DataAccessFailure
{
    ConcurrentModification,
    Duplicate,
    Constraint,
    ValueTooLong,
    Deadlock,
    DatabaseError
}

public static class DataAccessFailureClassifier
{
    public static bool TryClassify(Exception exception, out DataAccessFailure failure)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            failure = DataAccessFailure.ConcurrentModification;
            return true;
        }

        var sql = exception switch
        {
            DbUpdateException { InnerException: SqlException inner } => inner,
            SqlException direct => direct,
            _ => null
        };
        if (sql is null)
        {
            failure = default;
            return false;
        }

        failure = sql.Number switch
        {
            2601 or 2627 => DataAccessFailure.Duplicate,
            547 => DataAccessFailure.Constraint,
            2628 or 8152 or 8115 => DataAccessFailure.ValueTooLong,
            1205 => DataAccessFailure.Deadlock,
            _ => DataAccessFailure.DatabaseError
        };
        return true;
    }
}
