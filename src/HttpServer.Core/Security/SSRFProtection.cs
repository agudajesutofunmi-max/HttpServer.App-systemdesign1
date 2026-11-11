namespace HttpServer.Core.Security;

using HttpServer.Core.Models;

public class SSRFProtection
{
    private readonly ProxyConfig _config;

    public SSRFProtection(ProxyConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public bool IsUrlAllowed(string url)
    {
        if (_config.AllowedUpstreamUrls.Count == 0)
            return true;

        try
        {
            var requestUri = new Uri(url, UriKind.Absolute);

            foreach (var allowedUrl in _config.AllowedUpstreamUrls)
            {
                var allowedUri = new Uri(allowedUrl, UriKind.Absolute);

                if (requestUri.Host.Equals(allowedUri.Host, StringComparison.OrdinalIgnoreCase) &&
                    requestUri.Scheme.Equals(allowedUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
                    requestUri.Port == allowedUri.Port)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool IsPrivateNetwork(string url)
    {
        try
        {
            var uri = new Uri(url, UriKind.Absolute);
            var hostAddress = System.Net.Dns.GetHostAddresses(uri.Host).FirstOrDefault();

            if (hostAddress == null)
                return false;

            return System.Net.IPAddress.IsLoopback(hostAddress) ||
                   (hostAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    (hostAddress.ToString().StartsWith("10.") ||
                     hostAddress.ToString().StartsWith("192.168.") ||
                     hostAddress.ToString().StartsWith("172."))) ||
                   (hostAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
                    hostAddress.IsIPv6LinkLocal);
        }
        catch
        {
            return false;
        }
    }
}
