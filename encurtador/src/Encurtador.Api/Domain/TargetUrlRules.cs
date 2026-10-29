namespace Encurtador.Api.Domain;

public static class TargetUrlRules
{
    public static bool IsValid(string? targetUrl)
    {
        if (string.IsNullOrEmpty(targetUrl) || targetUrl.Length > 2048 ||
            !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.IsWellFormedOriginalString() &&
            (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }
}
