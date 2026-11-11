namespace HttpServer.Core.Models;

public class ProxyConfig
{
    public List<BackendServer> BackendServers { get; set; } = [];
    public LoadBalancingStrategy Strategy { get; set; } = LoadBalancingStrategy.RoundRobin;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    public int HealthCheckTimeoutSeconds { get; set; } = 5;
    public int MaxConsecutiveFailures { get; set; } = 3;
    public int ConnectionPoolSize { get; set; } = 100;
    public bool TrustXForwardedFor { get; set; } = true;
    public List<string> AllowedUpstreamUrls { get; set; } = [];
    public List<string> HopByHopHeaders { get; set; } =
    [
        "connection", "keep-alive", "proxy-authenticate",
        "proxy-authorization", "te", "trailers", "transfer-encoding", "upgrade"
    ];
}

public enum LoadBalancingStrategy
{
    RoundRobin,
    LeastConnections
}
