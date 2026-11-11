namespace HttpServer.Core.Headers;

using HttpServer.Core.Models;

public class HeaderManager
{
    private readonly ProxyConfig _config;

    public HeaderManager(ProxyConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void AddXForwardedHeaders(HttpRequestMessage request, string clientIp, string scheme, string host)
    {
        if (!_config.TrustXForwardedFor)
            return;

        request.Headers.Add("X-Forwarded-For", clientIp);
        request.Headers.Add("X-Forwarded-Proto", scheme);
        request.Headers.Add("X-Forwarded-Host", host);
    }

    public void RemoveHopByHopHeaders(HttpRequestMessage request)
    {
        foreach (var header in _config.HopByHopHeaders)
        {
            request.Headers.Remove(header);
            request.Content?.Headers.Remove(header);
        }
    }

    public void RemoveHopByHopHeadersFromResponse(HttpResponseMessage response)
    {
        foreach (var header in _config.HopByHopHeaders)
        {
            response.Headers.Remove(header);
            response.Content?.Headers.Remove(header);
        }
    }

    public void CopyResponseHeaders(HttpResponseMessage sourceResponse, Action<string, IEnumerable<string>> addHeaderAction)
    {
        foreach (var header in sourceResponse.Headers)
        {
            if (!_config.HopByHopHeaders.Contains(header.Key.ToLower()))
            {
                addHeaderAction(header.Key, header.Value);
            }
        }

        foreach (var header in sourceResponse.Content.Headers)
        {
            if (!_config.HopByHopHeaders.Contains(header.Key.ToLower()))
            {
                addHeaderAction(header.Key, header.Value);
            }
        }
    }
}
