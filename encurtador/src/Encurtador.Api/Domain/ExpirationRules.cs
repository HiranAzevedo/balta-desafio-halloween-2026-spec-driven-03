using System.Globalization;

namespace Encurtador.Api.Domain;

public enum ExpirationError
{
    None,
    InvalidFormat,
    PastDate,
    OutOfRange
}

public static class ExpirationRules
{
    public static bool TryParse(
        string? value,
        DateTime nowUtc,
        out DateOnly? expirationDate,
        out DateTime? exclusiveExpirationUtc,
        out ExpirationError error)
    {
        expirationDate = null;
        exclusiveExpirationUtc = null;
        error = ExpirationError.None;

        if (value is null)
        {
            return true;
        }

        if (!DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate))
        {
            error = ExpirationError.InvalidFormat;
            return false;
        }

        var todayUtc = DateOnly.FromDateTime(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc));
        if (parsedDate < todayUtc)
        {
            error = ExpirationError.PastDate;
            return false;
        }

        if (parsedDate == DateOnly.MaxValue)
        {
            error = ExpirationError.OutOfRange;
            return false;
        }

        expirationDate = parsedDate;
        exclusiveExpirationUtc = DateTime.SpecifyKind(
            parsedDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);
        return true;
    }
}
