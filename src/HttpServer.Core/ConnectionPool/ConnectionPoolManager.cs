namespace HttpServer.Core.ConnectionPool;

using System.Net;

public class ConnectionPoolManager : IDisposable
{
    private readonly HttpClientHandler _handler;
    private readonly HttpClient _httpClient;
    private readonly int _poolSize;

    public ConnectionPoolManager(int poolSize = 100)
    {
        _poolSize = poolSize;
        _handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = false,
            UseCookies = false,
            MaxConnectionsPerServer = poolSize
        };

        _httpClient = new HttpClient(_handler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public HttpClient GetClient()
    {
        return _httpClient;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _handler?.Dispose();
    }
}
