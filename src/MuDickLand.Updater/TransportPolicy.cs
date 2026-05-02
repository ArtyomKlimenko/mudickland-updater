namespace MuDickLand.Updater;

public static class TransportPolicy
{
    public static Uri RequireAllowedHttpUri(string rawUrl, string fieldName)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"{fieldName} is not a valid absolute URL.");
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return uri;
        }

        if (uri.Scheme == Uri.UriSchemeHttp && IsLocalHost(uri.Host))
        {
            return uri;
        }

        throw new InvalidOperationException($"{fieldName} must use HTTPS outside local testing.");
    }

    public static bool IsAllowedHttpUri(string rawUrl)
    {
        try
        {
            RequireAllowedHttpUri(rawUrl, "url");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLocalHost(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}

