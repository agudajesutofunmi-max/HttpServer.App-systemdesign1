namespace HttpServer.Core.LoadBalancing;

using HttpServer.Core.Interfaces;
using HttpServer.Core.Models;

public class LeastConnectionsBalancer : ILoadBalancer
{
    public BackendServer SelectServer(IReadOnlyList<BackendServer> servers)
    {
        var healthyServers = servers
            .Where(s => s.Status == HealthStatus.Healthy)
            .OrderBy(s => s.ActiveConnections)
            .ToList();

        if (healthyServers.Count == 0)
            throw new InvalidOperationException("No healthy backend servers available");

        return healthyServers.First();
    }

    public void ReleaseConnection(BackendServer server)
    {
        Interlocked.Decrement(ref server.ActiveConnections);
    }

    public void Reset()
    {
    }
}
