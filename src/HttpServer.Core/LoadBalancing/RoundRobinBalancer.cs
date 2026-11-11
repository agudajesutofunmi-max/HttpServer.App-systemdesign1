namespace HttpServer.Core.LoadBalancing;

using HttpServer.Core.Interfaces;
using HttpServer.Core.Models;

public class RoundRobinBalancer : ILoadBalancer
{
    private int _currentIndex = 0;
    private readonly object _lock = new();

    public BackendServer SelectServer(IReadOnlyList<BackendServer> servers)
    {
        var healthyServers = servers
            .Where(s => s.Status == HealthStatus.Healthy)
            .ToList();

        if (healthyServers.Count == 0)
            throw new InvalidOperationException("No healthy backend servers available");

        lock (_lock)
        {
            var server = healthyServers[_currentIndex % healthyServers.Count];
            _currentIndex++;
            return server;
        }
    }

    public void ReleaseConnection(BackendServer server)
    {
        Interlocked.Decrement(ref server.ActiveConnections);
    }

    public void Reset()
    {
        lock (_lock)
        {
            _currentIndex = 0;
        }
    }
}
