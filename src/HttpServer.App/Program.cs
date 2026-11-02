using HttpServer.App;

// ============================================================================
// CUSTOM HTTP SERVER - Project 1: Core Concepts & Security
// ============================================================================
// Goals:
//   - Build a TCP-based HTTP server from scratch (no HttpListener/Kestrel)
//   - Handle multiple concurrent connections (like goroutines in Go)
//   - Parse HTTP protocol correctly and prevent header-based attacks
//   - Implement keep-alive and timeouts for production-like behavior
//
// Security Angles:
//   - Slowloris attack: Clients sending headers slowly to exhaust resources
//   - Request smuggling: Ambiguous header parsing leading to desync
//   - Header size limits: Prevent memory exhaustion from massive headers
//   - Path traversal: Validate URL paths to prevent directory escape
// ============================================================================

var server = new HttpServerImplementation(port: 8080);

try
{
    await server.StartAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Server error: {ex.Message}");
    Environment.Exit(1);
}
