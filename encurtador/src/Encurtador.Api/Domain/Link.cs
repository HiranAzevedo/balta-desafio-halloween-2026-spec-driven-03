namespace Encurtador.Api.Domain;

public sealed class Link
{
    private Link()
    {
        Code = string.Empty;
        TargetUrl = string.Empty;
        ManagementKeyHash = [];
    }

    public Link(
        string code,
        string targetUrl,
        bool isCustomCode,
        DateTime createdAtUtc,
        DateTime? expiresAtUtc,
        byte[] managementKeyHash,
        long accessCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(accessCount);
        Code = code;
        TargetUrl = targetUrl;
        IsCustomCode = isCustomCode;
        CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
        ExpiresAtUtc = expiresAtUtc.HasValue
            ? DateTime.SpecifyKind(expiresAtUtc.Value, DateTimeKind.Utc)
            : null;
        AccessCount = accessCount;
        ManagementKeyHash = managementKeyHash.ToArray();
    }

    public long Id { get; private set; }

    public string Code { get; private set; }

    public string TargetUrl { get; private set; }

    public bool IsCustomCode { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public long AccessCount { get; private set; }

    public byte[] ManagementKeyHash { get; private set; }
}
