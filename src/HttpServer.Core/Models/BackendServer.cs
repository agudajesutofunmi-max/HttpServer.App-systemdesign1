namespace HttpServer.Core.Models;

public class BackendServer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Url { get; set; } = string.Empty;
    public string HealthCheckPath { get; set; } = "/health";
    public HealthStatus Status { get; set; } = HealthStatus.Healthy;
    public int ActiveConnections = 0;
    public DateTime LastHealthCheckTime { get; set; } = DateTime.UtcNow;
    public int ConsecutiveFailures { get; set; }
    public int Weight { get; set; } = 1;
}

public enum HealthStatus
{
    Healthy,
    Unhealthy,
    Checking
}
