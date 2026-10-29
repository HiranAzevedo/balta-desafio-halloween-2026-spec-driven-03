using Encurtador.Api.Domain;

namespace Encurtador.Tests.Unit;

public sealed class ExpirationRulesTests
{
    private static readonly DateTime NowUtc =
        new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TryParse_accepts_today_and_uses_exclusive_next_midnight()
    {
        var valid = ExpirationRules.TryParse(
            "2026-10-29",
            NowUtc,
            out var date,
            out var expiresAtUtc,
            out var error);

        Assert.True(valid);
        Assert.Equal(new DateOnly(2026, 10, 29), date);
        Assert.Equal(new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc), expiresAtUtc);
        Assert.Equal(ExpirationError.None, error);
    }

    [Fact]
    public void TryParse_rejects_a_date_before_today_in_utc()
    {
        var valid = ExpirationRules.TryParse(
            "2026-10-28",
            NowUtc,
            out _,
            out _,
            out var error);

        Assert.False(valid);
        Assert.Equal(ExpirationError.PastDate, error);
    }

    [Theory]
    [InlineData("29-10-2026")]
    [InlineData("2026-13-01")]
    [InlineData("2026-10-29T00:00:00Z")]
    [InlineData("9999-12-31")]
    public void TryParse_rejects_invalid_or_unrepresentable_expiration_dates(string value)
    {
        var valid = ExpirationRules.TryParse(value, NowUtc, out _, out _, out var error);

        Assert.False(valid);
        Assert.NotEqual(ExpirationError.None, error);
    }

    [Fact]
    public void TryParse_accepts_null_as_no_expiration()
    {
        var valid = ExpirationRules.TryParse(
            null,
            NowUtc,
            out var date,
            out var expiresAtUtc,
            out var error);

        Assert.True(valid);
        Assert.Null(date);
        Assert.Null(expiresAtUtc);
        Assert.Equal(ExpirationError.None, error);
    }
}
