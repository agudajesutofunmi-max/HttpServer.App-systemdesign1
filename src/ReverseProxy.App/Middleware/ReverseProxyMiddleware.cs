namespace ReverseProxy.App.Middleware;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using HttpServer.Core.ConnectionPool;
using HttpServer.Core.Headers;
using HttpServer.Core.Interfaces;
using HttpServer.Core.Models;
using HttpServer.Core.Security;

public class ReverseProxyMiddleware
{
    private readonly RequestDelegate _next;

    public ReverseProxyMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(
        HttpContext context,
        ILoadBalancer loadBalancer,
        ProxyConfig config,
        ConnectionPoolManager connectionPool,
        SSRFProtection ssrfProtection,
        HeaderManager headerManager)
    {
        if (!IsProxyableRequest(context.Request))
        {
            await _next(context);
            return;
        }

        try
        {
            var backend = loadBalancer.SelectServer(config.BackendServers);

            var upstreamUrl = BuildUpstreamUrl(backend.Url, context.Request);

            if (!ssrfProtection.IsUrlAllowed(upstreamUrl))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { error = "URL not allowed" });
                return;
            }

            Interlocked.Increment(ref backend.ActiveConnections);

            try
            {
                var httpClient = connectionPool.GetClient();
                var request = BuildProxyRequest(context.Request, upstreamUrl);

                headerManager.RemoveHopByHopHeaders(request);

                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                headerManager.AddXForwardedHeaders(request, clientIp, context.Request.Scheme, context.Request.Host.ToString());

                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                headerManager.RemoveHopByHopHeadersFromResponse(response);
                context.Response.StatusCode = (int)response.StatusCode;

                headerManager.CopyResponseHeaders(response, (key, values) =>
                {
                    context.Response.Headers.Append(key, values.ToArray());
                });

                if (response.Content != null)
                {
                    using var stream = await response.Content.ReadAsStreamAsync();
                    await stream.CopyToAsync(context.Response.Body);
                }
            }
            finally
            {
                loadBalancer.ReleaseConnection(backend);
            }
        }
        catch (InvalidOperationException ex)
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsJsonAsync(new { error = "Service unavailable", details = ex.Message });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "Internal server error", details = ex.Message });
        }
    }

    private bool IsProxyableRequest(HttpRequest request)
    {
        var nonProxyablePathPrefixes = new[] { "/health", "/metrics", "/admin" };
        return !nonProxyablePathPrefixes.Any(prefix =>
            request.Path.StartsWithSegments(prefix, System.StringComparison.OrdinalIgnoreCase)
        );
    }

    private string BuildUpstreamUrl(string backendUrl, HttpRequest request)
    {
        var backendUri = backendUrl.TrimEnd('/');
        var path = request.Path + request.QueryString;
        return $"{backendUri}{path}";
    }

    private HttpRequestMessage BuildProxyRequest(HttpRequest request, string upstreamUrl)
    {
        var proxyRequest = new HttpRequestMessage
        {
            Method = new HttpMethod(request.Method),
            RequestUri = new Uri(upstreamUrl, UriKind.Absolute)
        };

        if (request.Body.CanRead && 
            (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) || 
             request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase) || 
             request.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
        {
            proxyRequest.Content = new StreamContent(request.Body);

            if (request.ContentType != null)
            {
                proxyRequest.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(request.ContentType);
            }
        }

        foreach (var header in request.Headers)
        {
            if (!header.Key.Equals("Host", System.StringComparison.OrdinalIgnoreCase) &&
                !header.Key.Equals("Connection", System.StringComparison.OrdinalIgnoreCase))
            {
                if (header.Key.Equals("Content-Length", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        return proxyRequest;
    }
}
