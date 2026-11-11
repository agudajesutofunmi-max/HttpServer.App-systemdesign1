namespace HttpServer.Core.Models;

public class LoadBalancingRequest
{
    public BackendServer SelectedServer { get; set; } = null!;
    public DateTime SelectionTime { get; set; } = DateTime.UtcNow;
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
}
