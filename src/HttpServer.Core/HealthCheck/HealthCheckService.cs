namespace HttpServer.Core.HealthCheck;

using HttpServer.Core.Models;

public class HealthCheckService
{
    private readonly HttpClient _httpClient;
    private readonly ProxyConfig _config;
    private readonly Timer _timer;
    private readonly object _lock = new();

    public HealthCheckService(HttpClient httpClient, ProxyConfig config)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _timer = new Timer(
            _ => CheckAllServersHealth(),
            null,
            TimeSpan.FromSeconds(_config.HealthCheckIntervalSeconds),
            TimeSpan.FromSeconds(_config.HealthCheckIntervalSeconds)
        );
    }

    private void CheckAllServersHealth()
    {
        var tasks = _config.BackendServers
            .Where(s => s.Status != HealthStatus.Checking)
            .Select(server => CheckServerHealthAsync(server))
            .ToList();

        Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(_config.HealthCheckTimeoutSeconds));
    }

    private async Task CheckServerHealthAsync(BackendServer server)
    {
        lock (_lock)
        {
            if (server.Status == HealthStatus.Checking)
                return;

            server.Status = HealthStatus.Checking;
        }

        try
        {
            var url = $"{server.Url.TrimEnd('/')}{server.HealthCheckPath}";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.HealthCheckTimeoutSeconds));

            var response = await _httpClient.GetAsync(url, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                lock (_lock)
                {
                    server.Status = HealthStatus.Healthy;
                    server.ConsecutiveFailures = 0;
                    server.LastHealthCheckTime = DateTime.UtcNow;
                }
            }
            else
            {
                IncrementFailureCount(server);
            }
        }
        catch
        {
            IncrementFailureCount(server);
        }
    }

    private void IncrementFailureCount(BackendServer server)
    {
        lock (_lock)
        {
            server.ConsecutiveFailures++;

            if (server.ConsecutiveFailures >= _config.MaxConsecutiveFailures)
            {
                server.Status = HealthStatus.Unhealthy;
            }
            else
            {
                server.Status = HealthStatus.Healthy;
            }

            server.LastHealthCheckTime = DateTime.UtcNow;
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
