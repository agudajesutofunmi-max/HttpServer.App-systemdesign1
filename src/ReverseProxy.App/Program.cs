using Microsoft.AspNetCore.Builder;
using HttpServer.Core.ConnectionPool;
using HttpServer.Core.Headers;
using HttpServer.Core.HealthCheck;
using HttpServer.Core.Interfaces;
using HttpServer.Core.LoadBalancing;
using HttpServer.Core.Models;
using HttpServer.Core.Security;
using ReverseProxy.App.Middleware;

var builder = WebApplication.CreateBuilder(args);

var config = new ProxyConfig
{
    BackendServers =
    [
        new BackendServer { Url = "http://localhost:5001", HealthCheckPath = "/health" },
        new BackendServer { Url = "http://localhost:5002", HealthCheckPath = "/health" }
    ],
    Strategy = LoadBalancingStrategy.RoundRobin,
    HealthCheckIntervalSeconds = 30,
    HealthCheckTimeoutSeconds = 5,
    MaxConsecutiveFailures = 3,
    ConnectionPoolSize = 100,
    TrustXForwardedFor = true,
    AllowedUpstreamUrls = new List<string>
    {
        "http://localhost:5001",
        "http://localhost:5002"
    }
};

builder.Services.AddSingleton(config);
builder.Services.AddSingleton<ConnectionPoolManager>(sp =>
    new ConnectionPoolManager(config.ConnectionPoolSize));
builder.Services.AddSingleton<HttpClient>(sp =>
    sp.GetRequiredService<ConnectionPoolManager>().GetClient());
builder.Services.AddSingleton<ILoadBalancer>(sp =>
    config.Strategy == LoadBalancingStrategy.RoundRobin
        ? new RoundRobinBalancer()
        : new LeastConnectionsBalancer());
builder.Services.AddSingleton<HealthCheckService>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var service = new HealthCheckService(httpClient, config);
    return service;
});
builder.Services.AddSingleton<SSRFProtection>(sp => new SSRFProtection(config));
builder.Services.AddSingleton<HeaderManager>(sp => new HeaderManager(config));

var app = builder.Build();

app.UseMiddleware<ReverseProxyMiddleware>();

app.MapGet("/health", () => new { status = "ok" });

await app.RunAsync();
