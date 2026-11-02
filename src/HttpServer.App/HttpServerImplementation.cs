using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HttpServer.App;

/// <summary>
/// Core HTTP server implementation using raw TCP sockets.
/// 
/// Architecture:
/// - Accepts TCP connections on a specified port
/// - Spawns a Task per connection (equivalent to goroutines)
/// - Parses HTTP requests according to RFC 7230
/// - Supports keep-alive for connection reuse
/// - Enforces timeouts to prevent resource exhaustion
/// </summary>
public class HttpServerImplementation
{
    private const int Port = 8080;
    private const int MaxHeaderSize = 8192;        // 8KB - prevent Slowloris/header bombing
    private const int RequestTimeoutMs = 30000;    // 30s - max time to receive a complete request
    private const int KeepAliveTimeoutMs = 5000;   // 5s - time to wait for next request on keep-alive

    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;

    public HttpServerImplementation(int port = Port)
    {
        _port = port;
    }

    /// <summary>
    /// Starts the HTTP server and listens for incoming connections.
    /// This method runs indefinitely until cancellation is requested.
    /// </summary>
    public async Task StartAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _port);

        try
        {
            _listener.Start();
            Console.WriteLine($"[Server] Listening on port {_port}...");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // Accept incoming connection (blocking)
                    var client = await _listener.AcceptTcpClientAsync(_cancellationTokenSource.Token);

                    // Handle connection in a separate task (like spawning a goroutine)
                    _ = HandleClientAsync(client, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // Server shutting down
                    break;
                }
            }
        }
        finally
        {
            _listener?.Stop();
            _cancellationTokenSource?.Dispose();
            Console.WriteLine("[Server] Stopped.");
        }
    }

    /// <summary>
    /// Handles a single client connection.
    /// Supports HTTP/1.1 keep-alive, meaning multiple requests can be sent on one connection.
    /// 
    /// Security considerations:
    /// - Timeout on initial connection to prevent Slowloris attacks
    /// - Shorter timeout on keep-alive reads to prevent resource hogging
    /// - Header size limit to prevent DoS
    /// </summary>
    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        using (var networkStream = client.GetStream())
        {
            var remoteEndpoint = client.Client.RemoteEndPoint;
            Console.WriteLine($"[Client] Connected from {remoteEndpoint}");

            try
            {
                // ===================================================================
                // KEEP-ALIVE LOOP: Process multiple requests on same connection
                // ===================================================================
                bool keepAlive = true;
                int requestCount = 0;

                while (keepAlive && !cancellationToken.IsCancellationRequested)
                {
                    requestCount++;

                    // TODO: PHASE 1 - Parse HTTP Request
                    // - Read request line (GET /path HTTP/1.1\r\n)
                    // - Read headers (Key: Value\r\n)
                    // - Validate headers don't exceed MaxHeaderSize (prevent header bombs)
                    // - Read body if present (use Content-Length or chunked encoding)
                    // - Detect request smuggling attempts (duplicate Content-Length, etc.)

                    var request = await ParseHttpRequestAsync(networkStream, RequestTimeoutMs, cancellationToken);

                    if (request == null)
                    {
                        break; // Client disconnected or malformed request
                    }

                    // TODO: PHASE 2 - Route & Handle Request
                    // - Match request path to a handler
                    // - Return appropriate response (200, 404, 405, etc.)
                    // - Log request details for monitoring/debugging

                    var response = HandleRequest(request);

                    // TODO: PHASE 2 - Send HTTP Response
                    // - Write status line (HTTP/1.1 200 OK\r\n)
                    // - Write response headers
                    // - Write body
                    // - Flush to ensure client receives it

                    await SendHttpResponseAsync(networkStream, response, cancellationToken);

                    // ===================================================================
                    // KEEP-ALIVE DECISION: Should we wait for another request?
                    // ===================================================================
                    // Check if client wants to close or if we should close
                    keepAlive = ShouldKeepAlive(request, response);

                    if (keepAlive)
                    {
                        Console.WriteLine($"[Client] Keep-alive enabled, waiting for request #{requestCount + 1}...");
                        // NOTE: A timeout here prevents the connection from hanging forever idle
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[Client] Request timeout from {remoteEndpoint}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] Error handling {remoteEndpoint}: {ex.Message}");
            }
            finally
            {
                Console.WriteLine($"[Client] Disconnected from {remoteEndpoint}");
            }
        }
    }

    /// <summary>
    /// Parses an HTTP request from the network stream.
    /// 
    /// HTTP Request Format (RFC 7230):
    /// ```
    /// GET /path HTTP/1.1\r\n
    /// Host: example.com\r\n
    /// Content-Length: 10\r\n
    /// \r\n
    /// [body bytes]
    /// ```
    /// 
    /// Security: Enforce timeouts and size limits to prevent DoS attacks.
    /// </summary>
    


        // TODO: IMPLEMENT
        // 1. Read request line with timeout (e.g., "GET / HTTP/1.1")
        //    - Use CancellationToken to enforce timeouts
        //    - Protect against Slowloris: enforce timeout on reading
        // 2. Parse request method, URI, HTTP version
        //    - Validate HTTP version (1.0, 1.1, 2.0)
        //    - Validate method (GET, POST, PUT, DELETE, HEAD, etc.)
        //    - Validate URI format (prevent path traversal attacks here)
        // 3. Read headers until blank line (\r\n\r\n)
        //    - Check total header size doesn't exceed MaxHeaderSize
        //    - Handle folded headers (continuation lines with space/tab)
        //    - Normalize header names (case-insensitive per RFC)
        //    - Detect Request Smuggling attempts:
        //      * Duplicate Content-Length headers
        //      * Both Content-Length and Transfer-Encoding headers
        // 4. Read body if present
        //    - Use Content-Length to know how many bytes to read
        //    - Or handle chunked Transfer-Encoding


        private async Task<HttpRequest?> ParseHttpRequestAsync(NetworkStream stream, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);
            var linkedToken = cts.Token;

            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

            // 1. Read request line: GET / HTTP/1.1
            var requestLine = await reader.ReadLineAsync().WaitAsync(linkedToken);
            if (string.IsNullOrWhiteSpace(requestLine))
                return null;

            Console.WriteLine($"[Request] {requestLine}");

            var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
                return null;

            var method = parts[0].Trim().ToUpper();
            var path = parts[1].Trim();
            var version = parts[2].Trim();

            // Validate HTTP version
            if (version != "HTTP/1.0" && version != "HTTP/1.1" && version != "HTTP/2.0" && version != "HTTP/2")
                return null;

            // Validate method
            var validMethods = new[] { "GET", "POST", "PUT", "DELETE", "HEAD", "OPTIONS", "PATCH" };
            if (!validMethods.Contains(method))
                return null;

            // Prevent path traversal attack: e.g. /../../etc/passwd
            if (path.Contains("..") || !path.StartsWith("/"))
                return null;

            // 3. Read headers until blank line
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int totalHeaderSize = requestLine.Length;
            string? lastHeaderKey = null;
            int contentLengthCount = 0;
            bool hasTransferEncoding = false;

            while (true)
            {
                var line = await reader.ReadLineAsync().WaitAsync(linkedToken);
                if (line == null) break;
                if (line == "") break; // Blank line = end of headers

                totalHeaderSize += line.Length;
                if (totalHeaderSize > MaxHeaderSize) // from your checklist - 8KB
                    return null;

                // Handle folded headers (line starts with space/tab)
                if ((line.StartsWith(" ") || line.StartsWith("\t")) && lastHeaderKey != null)
                {
                    headers[lastHeaderKey] += " " + line.Trim();
                    continue;
                }

                var colonIdx = line.IndexOf(':');
                if (colonIdx <= 0) return null;

                var key = line.Substring(0, colonIdx).Trim();
                var value = line.Substring(colonIdx + 1).Trim();

                // Detect smuggling
                if (key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    contentLengthCount++;
                if (key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                    hasTransferEncoding = true;

                // Normalize key
                headers[key] = value;
                lastHeaderKey = key;
            }

            // Detect request smuggling attempts
            if (contentLengthCount > 1) return null;
            if (contentLengthCount > 0 && hasTransferEncoding) return null;

            // 4. Read body if Content-Length
            string body = "";
            if (headers.TryGetValue("Content-Length", out var clValue) && int.TryParse(clValue, out var contentLength) && contentLength > 0)
            {
                if (contentLength > 1_000_000) return null; // 1MB limit
                var buffer = new char[contentLength];
                int read = 0;
                while (read < contentLength)
                {
                    int r = await reader.ReadAsync(buffer, read, contentLength - read).WaitAsync(linkedToken);
                    if (r == 0) break;
                    read += r;
                }
                body = new string(buffer, 0, read);
            }

            return new HttpRequest
            {
                Method = method,
                Path = path,
                HttpVersion = version,
                Headers = headers,
                Body = body
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Processes an HTTP request and returns a response.
    /// Currently a stub that should be expanded to:
    /// - Route requests to handlers based on path
    /// - Apply middleware (logging, auth, etc.)
    /// - Return appropriate status codes
    /// </summary>



    // TODO: PHASE 2 - IMPLEMENT ROUTING
    // Example:
    // - GET /health -> 200 OK with {"status":"healthy"}
    // - GET /echo -> Echo back request info
    // - GET /* -> 404 Not Found
    //
    // Consider path traversal attacks:
    // - /../../etc/passwd -> exploit (must be blocked)
    // - Normalize paths (collapse .., remove double slashes, etc.)


    private HttpResponse HandleRequest(HttpRequest request)
    {
        // Log incoming request for debugging - O(1)
        Console.WriteLine($"[Request] {request.Method} {request.Path} {request.HttpVersion}");

        // ================= STEP 1: PATH NORMALIZATION =================
        // Algorithm: Input sanitization - prevent attackers from manipulating path
        // Why: URL can be /health?token=123, we only want /health
        // Time: O(n) where n = path length (Split scans string once)
        var rawPath = request.Path.Split('?')[0]; // Remove query string: /health?x=1 -> /health

        // Algorithm: String collapse - // -> / 
        // Why: /echo//test and /echo/test should be same. Prevent bypass tricks.
        // Example attack: /health//../etc/passwd
        // Time: O(n)
        rawPath = rawPath.Replace("//", "/");

        // ================= STEP 2: SECURITY CHECK - PATH TRAVERSAL =================
        // Algorithm: Blacklist pattern matching
        // Threat: ../ means "go up one directory" -> /../../etc/passwd tries to read server files
        // This is how real hackers hacked early PHP sites
        // Real world: AWS S3, Nginx all do this check
        // Time: O(n*m) where m = pattern length, but .Contains is optimized to O(n)
        if (rawPath.Contains("..") || rawPath.Contains("/etc/passwd"))
        {
            // Return 400 Bad Request - fail fast, don't process further
            // Algorithm principle: Fail securely
            return new HttpResponse
            {
                StatusCode = 400,
                StatusMessage = "Bad Request",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
                Body = "400 Bad Request: Invalid path"
            };
        }

        // ================= STEP 3: PATH CANONICALIZATION =================
        // Algorithm: Normalization to canonical form
        // Why: /Health/ , /health, /HEALTH should all match same route (case-insensitive)
        // TrimEnd('/') : /health/ -> /health so /health and /health/ are same
        // ToLower() : Case-insensitive matching
        // If empty after trim (was just "/"), set to "/"
        // Time: O(n)
        var path = rawPath.TrimEnd('/').ToLower();
        if (string.IsNullOrEmpty(path)) path = "/";

        // ================= STEP 4: ROUTING TABLE - O(1) hash-like lookup =================
        // Algorithm: Linear search through routes (for few routes O(k) where k = num routes = 3)
        // For 1000 routes, we'd use Dictionary<string, Func> for O(1) lookup like ASP.NET does
        // Order matters: Most specific to least specific (like switch-case)

        // ROUTE 1: /health - Health check endpoint
        // Algorithm: Exact match O(1) comparison
        // Purpose: Load balancers (like Jumia's) ping /health to know if server is alive
        // If 200 -> keep server in pool, else remove it
        if (path == "/health")
        {
            var json = "{\"status\":\"healthy\"}";
            // Note: Content-Length must be BYTE count, not char count (for UTF8)
            return new HttpResponse
            {
                StatusCode = 200,
                StatusMessage = "OK",
                Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Content-Length", Encoding.UTF8.GetByteCount(json).ToString() }
            },
                Body = json
            };
        }

        // ROUTE 2: /echo - Debugging endpoint
        // Algorithm: Prefix match - /echo, /echo/foo, /echo/bar all match
        // Purpose: Reflects what server received - helps frontend debug what backend saw
        // Similar to httpbin.org/anything used by Paystack devs
        // Time: O(n) for StartsWith check
        if (path == "/echo" || path.StartsWith("/echo/"))
        {
            // Algorithm: String building with StringBuilder (O(n) total)
            // Why StringBuilder not string += ? string += is O(n^2) because it copies each time
            // StringBuilder is O(n) amortized - important when headers are many
            var sb = new StringBuilder();
            sb.AppendLine($"Method: {request.Method}");
            sb.AppendLine($"Path: {request.Path}");
            sb.AppendLine($"Version: {request.HttpVersion}");
            sb.AppendLine("Headers:");
            // Algorithm: Iterate dictionary O(h) where h = number of headers
            foreach (var h in request.Headers)
                sb.AppendLine($"  {h.Key}: {h.Value}");
            sb.AppendLine($"Body: {request.Body}");

            var body = sb.ToString();
            return new HttpResponse
            {
                StatusCode = 200,
                StatusMessage = "OK",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
                Body = body
            };
        }

        // ROUTE 3: / - Root route
        // Algorithm: Exact match - default landing
        if (path == "/")
        {
            return new HttpResponse
            {
                StatusCode = 200,
                StatusMessage = "OK",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
                Body = "Hello from custom HTTP server!"
            };
        }

        // ROUTE 4: CATCH-ALL 404 - No route matched
        // Algorithm: Default case - like else in if-else chain
        // Why 404 not 400? 400 = client sent bad syntax, 404 = syntax ok but resource doesn't exist
        // This follows HTTP spec RFC 7231
        return new HttpResponse
        {
            StatusCode = 404,
            StatusMessage = "Not Found",
            Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
            Body = "404 Not Found"
        };
    }

    /// <summary>
    /// Sends an HTTP response to the client.
    /// Formats according to RFC 7230.
    /// </summary>
    private async Task SendHttpResponseAsync(
        NetworkStream stream,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        // TODO: IMPLEMENT
        // 1. Write status line: "HTTP/1.1 200 OK\r\n"
        // 2. Write response headers: "Content-Type: text/html\r\n"
        // 3. Write blank line: "\r\n"
        // 4. Write body bytes
        // 5. Flush stream

        var statusLine = $"HTTP/1.1 {response.StatusCode} {response.StatusMessage}\r\n";
        var headerLines = string.Join("\r\n", response.Headers.Select(h => $"{h.Key}: {h.Value}"));
        var body = response.Body ?? "";
        var bodyBytesLength = System.Text.Encoding.UTF8.GetByteCount(body);

        string responseText;
        if (string.IsNullOrEmpty(headerLines))
            responseText = $"{statusLine}Content-Length: {bodyBytesLength}\r\n\r\n{body}";
        else
            responseText = $"{statusLine}{headerLines}\r\nContent-Length: {bodyBytesLength}\r\n\r\n{body}";

        var responseBytes = System.Text.Encoding.UTF8.GetBytes(responseText);

        await stream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        Console.WriteLine($"[Response] {response.StatusCode} {response.StatusMessage}");
    }

    /// <summary>
    /// Determines if the connection should remain open for another request.
    /// 
    /// HTTP/1.1 defaults to keep-alive UNLESS:
    /// - Connection: close header is present
    /// - HTTP/1.0 AND Connection: keep-alive NOT present
    /// - Server decides to close (e.g., max requests reached)
    /// </summary>
    private bool ShouldKeepAlive(HttpRequest request, HttpResponse response)
    {
        // TODO: IMPLEMENT
        // 1. Check for Connection: close header in request
        // 2. Check for Connection: close header in response
        // 3. Default based on HTTP version (1.1 = keep-alive, 1.0 = close)

        // Placeholder: Always allow keep-alive for now
        return true;
    }
}

/// <summary>
/// Represents an HTTP request.
/// This is a simplified model. Real servers would track more details.
/// </summary>
public class HttpRequest
{
    public required string Method { get; init; }      // GET, POST, PUT, DELETE, HEAD, OPTIONS, etc.
    public required string Path { get; init; }        // /path/to/resource
    public required string HttpVersion { get; init; } // HTTP/1.0, HTTP/1.1, HTTP/2, etc.
    public required Dictionary<string, string> Headers { get; init; }
    public string? Body { get; init; }

    public string? GetHeader(string name)
    {
        // Headers are case-insensitive per RFC 7230
        return Headers.FirstOrDefault(h => 
            h.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
    }
}

/// <summary>
/// Represents an HTTP response.
/// </summary>
public class HttpResponse
{
    public required int StatusCode { get; init; }      // 200, 404, 500, etc.
    public required string StatusMessage { get; init; } // OK, Not Found, Internal Server Error, etc.
    public required Dictionary<string, string> Headers { get; init; }
    public string? Body { get; init; }
}
