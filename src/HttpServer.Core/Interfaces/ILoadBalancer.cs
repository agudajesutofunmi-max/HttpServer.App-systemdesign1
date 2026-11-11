namespace HttpServer.Core.Interfaces;

using HttpServer.Core.Models;

public interface ILoadBalancer
{
    BackendServer SelectServer(IReadOnlyList<BackendServer> servers);
    void ReleaseConnection(BackendServer server);
    void Reset();
}
