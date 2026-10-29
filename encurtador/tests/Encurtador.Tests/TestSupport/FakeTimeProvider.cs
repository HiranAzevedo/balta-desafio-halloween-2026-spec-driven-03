namespace Encurtador.Tests.TestSupport;

public sealed class FakeTimeProvider(DateTimeOffset initialUtc) : TimeProvider
{
    private long utcTicks = initialUtc.UtcTicks;

    public override DateTimeOffset GetUtcNow() =>
        new(Interlocked.Read(ref utcTicks), TimeSpan.Zero);

    public void SetUtcNow(DateTimeOffset value) =>
        Interlocked.Exchange(ref utcTicks, value.UtcTicks);
}
