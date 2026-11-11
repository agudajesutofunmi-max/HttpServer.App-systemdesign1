// ============================================================================
// PROJECT 1: CUSTOM HTTP SERVER - PROGRESS & SECURITY ROADMAP
// ============================================================================
//
// CURRENT STATUS: Foundation scaffolded with comments/TODOs
// TARGET: Production-grade HTTP/1.1 server with security hardening
//
// ============================================================================
// PHASE 1: TCP & HTTP PARSING (FOUNDATION)
// ============================================================================
// 
// [TODO] 1. Implement ParseHttpRequestAsync()
//    File: HttpServerImplementation.cs, line ~140
//    Goal: Parse raw bytes into HttpRequest object
//    
//    Steps:
//    a) Read request line (blocking read with timeout)
//       - Use BinaryReader or NetworkStream.Read()
//       - Enforce timeout via CancellationToken with Task.Delay
//       - Line format: "GET / HTTP/1.1\r\n"
//       
//    b) Parse line into method, path, version
//       - Split on spaces: parts[0]=method, parts[1]=path, parts[2]=version
//       - Validate method (must be upper-case, one of: GET, POST, PUT, DELETE, HEAD, OPTIONS, PATCH)
//       - Validate version ("HTTP/1.0" or "HTTP/1.1") - reject HTTP/2 for now
//       
//    c) Read headers until blank line
//       - Keep reading lines until you see "\r\n" (blank line = just \r\n)
//       - Parse each header: "Content-Type: application/json\r\n"
//       - Strip \r\n from each line
//       - Accumulate total size; reject if > MaxHeaderSize (prevent DoS)
//       - Store in case-insensitive dictionary
//       
//    d) Check for request body
//       - Read Content-Length header (if present)
//       - If Content-Length: 100, read exactly 100 bytes
//       - If Transfer-Encoding: chunked, decode chunked format
//       - Return HttpRequest object
//
//    Security Checks:
//    ✓ [SLOWLORIS] Timeout on read prevents slow-send attacks
//    ✓ [HEADER BOMB] MaxHeaderSize limit prevents memory exhaustion
//    ✓ [REQUEST SMUGGLING] Check for duplicate Content-Length, CL + TE combo
//
//
// [TODO] 2. Implement SendHttpResponseAsync()
//    File: HttpServerImplementation.cs, line ~230
//    Goal: Convert HttpResponse back to bytes and send over network
//    
//    Format (RFC 7230):
//    ```
//    HTTP/1.1 200 OK\r\n
//    Content-Type: text/html\r\n
//    Content-Length: 13\r\n
//    \r\n
//    Hello, World!
//    ```
//    
//    Steps:
//    a) Build status line: "HTTP/1.1 {code} {message}\r\n"
//    b) Add headers: "Key: Value\r\n" for each header
//    c) Ensure Content-Length is set (required for keep-alive)
//    d) Add blank line: "\r\n"
//    e) Add body bytes
//    f) Write to network stream
//    g) Flush to ensure delivery
//
//
// [TODO] 3. Implement ShouldKeepAlive()
//    File: HttpServerImplementation.cs, line ~260
//    Goal: Decide if connection stays open after response
//    
//    Logic (RFC 7230):
//    - If request has "Connection: close" → return false
//    - If response has "Connection: close" → return false
//    - If HTTP/1.0 AND no "Connection: keep-alive" → return false
//    - If HTTP/1.1 → return true (default keep-alive)
//    
//    This enables the keep-alive loop in HandleClientAsync()
//
//
// ============================================================================
// PHASE 2: ROUTING & HANDLERS (APPLICATION LOGIC)
// ============================================================================
//
// [TODO] 4. Implement routing system
//    Current: HandleRequest() returns static response
//    Goal: Route /health → HealthHandler, /echo → EchoHandler, etc.
//    
//    Approach:
//    a) Create IHttpHandler interface
//    b) Implement handlers: HealthHandler, NotFoundHandler, EchoHandler
//    c) Map routes in a dictionary: { "/health": healthHandler, "/echo": echoHandler }
//    d) Lookup and invoke handler in HandleRequest()
//    
//    Handlers should return HttpResponse with appropriate status code
//
//
// [TODO] 5. Path traversal protection
//    File: HttpServerImplementation.cs, HandleRequest()
//    Goal: Prevent paths like "/../../../etc/passwd"
//    
//    Steps:
//    a) Normalize path:
//       - Replace \\ with /
//       - Remove duplicate //
//       - Collapse .. (go up one level)
//    b) Ensure normalized path starts with /
//    c) Ensure normalized path doesn't start with /..
//    d) Example: "/static/../../../etc/passwd" → "/etc/passwd" (rejected!)
//
//
// ============================================================================
// PHASE 3: SECURITY HARDENING (PRODUCTION-READINESS)
// ============================================================================
//
// [TODO] 6. Request smuggling prevention
//    Currently: ParseHttpRequestAsync() has TODO comment
//    Goal: Detect and reject ambiguous requests
//    
//    Attack vectors:
//    a) Duplicate Content-Length:
//       Request:
//       Content-Length: 10
//       Content-Length: 5
//       [5 bytes of attacker payload] + [5 bytes smuggled as next request]
//       
//       Fix: Reject if multiple Content-Length headers with different values
//    
//    b) Content-Length + Transfer-Encoding:
//       Request:
//       Content-Length: 100
//       Transfer-Encoding: chunked
//       
//       Front-end might use Content-Length, back-end uses chunked → desync
//       Fix: Reject if both headers present
//    
//    c) Invalid chunked encoding:
//       Transfer-Encoding: chunked with malformed chunk sizes
//       Fix: Validate chunk format: "5\r\nhello\r\n"
//
//
// [TODO] 7. Timeout refinement
//    Current: RequestTimeoutMs = 30000 (30 seconds for whole request)
//    Current: KeepAliveTimeoutMs = 5000 (5 seconds waiting for next request)
//    
//    Refinement:
//    a) Break down timeout into phases:
//       - Request line: 5s (attacker can't trickle headers slowly)
//       - Per header: 1s (finish sending headers quickly)
//       - Body read: varies by Content-Length
//    b) Implement using Task.WhenAny(readTask, delayTask)
//       Example:
//       ```
//       var readTask = stream.ReadAsync(buffer, ...);
//       var timeoutTask = Task.Delay(timeoutMs);
//       var finished = await Task.WhenAny(readTask, timeoutTask);
//       if (finished == timeoutTask) throw new TimeoutException();
//       ```
//
//
// [TODO] 8. Logging & monitoring
//    Current: Simple Console.WriteLine() calls
//    Goal: Structured logging for debugging attacks
//    
//    Events to log:
//    - Client connect/disconnect
//    - Request parsing errors (malformed headers, size limits hit)
//    - Timeout events (Slowloris indicators)
//    - 400 Bad Request (potential attacks)
//    - Multiple failed requests in short time (rate limiting)
//
//
// ============================================================================
// TESTING STRATEGY
// ============================================================================
//
// Test file: tests/HttpServer.Tests/UnitTest1.cs
//
// [TODO] 9. Unit tests for critical functions
//    a) ParseHttpRequestAsync() tests:
//       - Valid GET request with headers
//       - Valid POST request with body
//       - Header size limit exceeded (should fail)
//       - Timeout on slow header send (should fail)
//       - Duplicate Content-Length (should fail)
//       - Content-Length + Transfer-Encoding (should fail)
//    
//    b) ShouldKeepAlive() tests:
//       - HTTP/1.1 without headers → true
//       - HTTP/1.0 without Connection header → false
//       - HTTP/1.1 with Connection: close → false
//       - HTTP/1.1 with Connection: keep-alive → true
//    
//    c) Path normalization tests:
//       - "/health" → "/health"
//       - "/static/../../../etc/passwd" → rejected
//       - "//double//slash" → "/double/slash"
//
//
// [TODO] 10. Integration tests
//    a) Spin up server on localhost:8081
//    b) Send real HTTP requests using HttpClient
//    c) Verify responses are correct
//    d) Test keep-alive: send 2 requests on same connection
//    e) Test timeouts: send partial request, verify timeout
//
//
// ============================================================================
// ATTACK SCENARIOS (What can go wrong?)
// ============================================================================
//
// ⚠️  SLOWLORIS ATTACK
//    Attacker: Opens connection, sends bytes very slowly
//    Example:
//    ```
//    GET / HTTP/1.1\r\n          <- sent after 5 seconds
//    Host: localhost\r\n         <- sent after 5 more seconds
//    X-Custom: data\r\n          <- sent after 5 more seconds (repeat many times)
//    ```
//    Result: Server allocates resources, never gets complete request
//    Defense: Timeout on read() + MaxHeaderSize limit
//
//
// ⚠️  REQUEST SMUGGLING
//    Attacker: Exploits different parsing between front-end and back-end
//    Example: Front-end uses Content-Length, backend uses Transfer-Encoding
//    Result: Request #2 intended for back-end is interpreted as part of #1
//    Defense: Normalize header parsing, detect CL + TE combo
//
//
// ⚠️  PATH TRAVERSAL
//    Attacker: Requests /static/../../../etc/passwd
//    Result: Server accidentally serves system files
//    Defense: Normalize and validate paths before file access
//
//
// ⚠️  HEADER BOMB
//    Attacker: Sends 1 GB of headers
//    Result: Server runs out of memory
//    Defense: MaxHeaderSize limit (8 KB default)
//
//
// ============================================================================
// PERFORMANCE CONSIDERATIONS
// ============================================================================
//
// - Async/await throughout (not blocking)
// - Tasks per connection (scales to thousands)
// - Efficient byte reading (use buffers, not string parsing where possible)
// - Keep-alive reduces connection overhead
// - Consider connection pooling for future reverse proxy project
//
//
// ============================================================================
// NEXT STEPS
// ============================================================================
//
// 1. Implement ParseHttpRequestAsync() with all security checks
// 2. Implement SendHttpResponseAsync()
// 3. Implement ShouldKeepAlive()
// 4. Test with curl/wget: curl http://localhost:8080/health
// 5. Add basic routing (/health, /echo)
// 6. Add tests for each component
// 7. Manual security testing (Slowloris simulation, etc.)
// 8. Performance testing (k concurrent connections)
//
// ============================================================================
