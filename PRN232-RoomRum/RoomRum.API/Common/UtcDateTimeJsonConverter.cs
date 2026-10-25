using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoomRum.API.Common;

/// <summary>
/// SQL Server DATETIME2 does not store a DateTimeKind, so EF Core materialises
/// timestamps with Kind = Unspecified. Serialising those directly produces
/// "2026-10-05T07:00:00" with no zone marker, and Dart's DateTime.parse treats
/// that as local time - shifting every timestamp by the device offset (+7h in
/// Vietnam). This converter pins the contract to UTC in both directions.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    private const string UtcFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public override DateTime Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateTime value,
        JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        writer.WriteStringValue(
            utc.ToString(UtcFormat, CultureInfo.InvariantCulture));
    }
}

public static class DateTimeExtensions
{
    /// <summary>
    /// Truncates to whole seconds.
    ///
    /// Every timestamp column in the v5.0 schema is DATETIME2(0), but
    /// DateTime.UtcNow and SYSUTCDATETIME() both carry 7 fractional digits.
    /// SQL Server ROUNDS when narrowing, so a value of ...59.5 is stored as
    /// ...00 of the NEXT second. That makes stored timestamps appear to be in
    /// the future, which trips the "cannot be in the future" checks in
    /// TR_Messages_Integrity (error 52181). Writing whole seconds removes the
    /// rounding step entirely, so the stored value never exceeds now.
    /// </summary>
    public static DateTime ToWholeSecond(this DateTime value)
    {
        var utc = value.AsUtc();

        return new DateTime(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            utc.Minute,
            utc.Second,
            DateTimeKind.Utc);
    }

    /// <summary>Current UTC instant truncated to a whole second.</summary>
    public static DateTime UtcNowToSecond()
    {
        return DateTime.UtcNow.ToWholeSecond();
    }

    /// <summary>
    /// Timestamp for a column named CreatedAt on INSERT.
    ///
    /// The v5.0 schema defaults every CreatedAt to SYSUTCDATETIME(). SQL Server
    /// ROUNDS when narrowing to DATETIME2(0), so a row created at ...59.6 gets
    /// CreatedAt = ...00 of the NEXT second - a future value. The API writes
    /// the later columns (UpdatedAt, PaidAt, LastMessageAt) truncated to whole
    /// seconds, so a write happening inside that same second is EARLIER than
    /// the rounded CreatedAt and the row CHECK rejects it:
    ///   CK_Conversations_LastMessage, CK_Appointments_UpdatedAt,
    ///   CK_Payments_UpdatedAt / CK_Payments_PaidAtRange,
    ///   CK_Listings_UpdatedAt, CK_Users_UpdatedAt.
    ///
    /// Setting CreatedAt explicitly with the same truncation keeps both sides
    /// of every comparison on the API's clock, so a second row written later in
    /// the same second can never precede it.
    /// </summary>
    public static DateTime UtcNowCreatedAt()
    {
        return UtcNowToSecond();
    }

    /// <summary>
    /// Marks an unspecified timestamp as UTC. Used when mapping entities to
    /// DTOs so the JSON payload always carries an explicit UTC marker.
    /// </summary>
    public static DateTime AsUtc(this DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    public static DateTime? AsUtc(this DateTime? value)
    {
        return value?.AsUtc();
    }

    /// <summary>
    /// Converts a client supplied timestamp to the UTC instant the database
    /// stores. DateTimeOffset carries the device offset, so a Vietnamese client
    /// sending 14:00+07:00 is stored as 07:00 UTC.
    /// </summary>
    public static DateTime ToUtcInstant(this DateTimeOffset value)
    {
        return value.UtcDateTime;
    }
}
