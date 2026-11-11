// ============================================================================
// TESTING & DEBUGGING GUIDE FOR HTTP SERVER PROJECT 1
// ============================================================================
//
// This file contains practical commands and scenarios to test your implementation
// as you build each phase.
//
// ============================================================================
// TESTING TOOLS
// ============================================================================
//
// 1. CURL (most common HTTP client tool)
//    ```powershell
//    # Simple GET request
//    curl http://localhost:8080/
//    
//    # With headers
//    curl -H "X-Custom-Header: value" http://localhost:8080/
//    
//    # POST with body
//    curl -X POST -d "key=value" http://localhost:8080/
//    
//    # Keep-alive test (multiple requests on same connection)
//    curl -v -K - <<EOF
//    url = "http://localhost:8080/request1"
//    url = "http://localhost:8080/request2"
//    EOF
//    
//    # Slow request (Slowloris test - DON'T USE IN PRODUCTION!)
//    (echo -n "GET / HTTP/1.1\r\nHost: localhost\r\n"; sleep 10; echo -e "\r\n") | nc localhost 8080
//    ```
//
// 2. POWERSHELL HttpClient
//    ```powershell
//    $response = Invoke-WebRequest -Uri "http://localhost:8080/" -Method GET
//    $response.StatusCode
//    $response.Content
//    ```
//
// 3. NETCAT / NC
//    ```powershell
//    # Send raw HTTP
//    echo "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n" | nc localhost 8080
//    
//    # Slow send test (Slowloris simulation)
//    (echo -n "GET / HTTP/1.1\r\nHost: localhost\r\n"; sleep 5; echo -e "\r\n") | nc localhost 8080
//    ```
//
//
// ============================================================================
// PHASE 1 TESTING: HTTP PARSING & KEEP-ALIVE
// ============================================================================
//
// TEST 1.1: Basic GET Request
// ─────────────────────────────
// Expected: Server receives request and sends 200 OK response
// 
// Command:
//   curl -v http://localhost:8080/
//
// Expected Output:
//   > GET / HTTP/1.1
//   > Host: localhost:8080
//   > User-Agent: curl/...
//   > Accept: */*
//   >
//   < HTTP/1.1 200 OK
//   < Content-Type: text/plain
//   < Content-Length: 32
//   <
//   Hello from custom HTTP server!
//
// Debugging:
//   - Check HttpServerImplementation.cs ParseHttpRequestAsync() correctly reads request line
//   - Verify SendHttpResponseAsync() formats HTTP/1.1 status line correctly
//   - Check NetworkStream.ReadAsync() properly handles \r\n terminators
//
//
// TEST 1.2: Request with Headers
// ───────────────────────────────
// Expected: Server parses all headers into Dictionary<string, string>
//
// Command:
//   curl.exe -v -H "Content-Type: application/json" -H "X-Custom: test" http://localhost:8080/
//
// Expected Output:
//   Console: [Request] GET / HTTP/1.1
//   Console shows headers were parsed correctly
//
// Debugging:
//   - In ParseHttpRequestAsync(), add logging for each header parsed
//   - Check case-insensitive header lookup works (GetHeader() method)
//
//
// TEST 1.3: POST with Body
// ────────────────────────
// Expected: Server reads Content-Length and reads exactly that many body bytes
//
// Command:
//   curl -X POST -d "key=value&foo=bar" -H "Content-Type: application/x-www-form-urlencoded" http://localhost:8080/
//
// Expected Output:
//   < HTTP/1.1 200 OK
//   Server console: [Request] POST / HTTP/1.1
//
// Debugging:
//   - Verify Content-Length header is read
//   - Verify body bytes are correctly extracted
//   - For "key=value&foo=bar" (16 bytes), read exactly 16 bytes
//
//
// TEST 1.4: Header Size Limit (SECURITY)
// ───────────────────────────────────
// Expected: Server rejects request with headers > MaxHeaderSize (8 KB)
// Should receive: 413 Payload Too Large (or 400 Bad Request)
//
// Command (PowerShell):
//   ```powershell
//   $headers = "GET / HTTP/1.1`r`nHost: localhost`r`n"
//   for ($i = 0; $i -lt 1000; $i++) {
//       $headers += "X-Bomb-$i: " + ("A" * 50) + "`r`n"
//   }
//   $headers += "`r`n"
//   $headers | nc localhost 8080
//   ```
//
// Expected Output:
//   HTTP/1.1 413 Payload Too Large
//   (or your custom error response)
//
// Debugging:
//   - In ParseHttpRequestAsync(), track totalHeaderSize
//   - Throw exception if totalHeaderSize > MaxHeaderSize
//   - Send 413 response instead of processing request
//
//
// TEST 1.5: Timeout on Slow Client (SECURITY - Slowloris Prevention)
// ──────────────────────────────────────────────────────────────────
// Expected: Server times out after RequestTimeoutMs, disconnects client
//
// Command:
//   ```powershell
//   # Open connection and send request line slowly
//   $socket = New-Object System.Net.Sockets.TcpClient("localhost", 8080)
//   $stream = $socket.GetStream()
//   $writer = New-Object System.IO.StreamWriter($stream)
//   
//   # Send first part of request
//   $writer.Write("GET / HTTP/1.1`r`n")
//   $writer.Flush()
//   
//   # Wait 35 seconds (longer than RequestTimeoutMs = 30s)
//   Start-Sleep -Seconds 35
//   
//   # Try to send headers (should fail, connection closed)
//   $writer.Write("Host: localhost`r`n")
//   $writer.Flush()
//   ```
//
// Expected Output:
//   [Client] Request timeout from 127.0.0.1:xxxxx
//   (connection closes after 30s)
//
// Debugging:
//   - Use Task.Delay() to enforce timeout via CancellationToken
//   - Wrap ParseHttpRequestAsync() in timeout check
//
//
// TEST 1.6: Keep-Alive (Multiple Requests on Same Connection)
// ────────────────────────────────────────────────────────
// Expected: Client sends 2 HTTP requests on single TCP connection
//           Server processes both
//           Connection remains open until client closes or Connection: close
//
// Command:
//   ```powershell
//   $socket = New-Object System.Net.Sockets.TcpClient("localhost", 8080)
//   $stream = $socket.GetStream()
//   $writer = New-Object System.IO.StreamWriter($stream)
//   $reader = New-Object System.IO.StreamReader($stream)
//   
//   # Request 1
//   $writer.WriteLine("GET /request1 HTTP/1.1")
//   $writer.WriteLine("Host: localhost")
//   $writer.WriteLine("")
//   $writer.Flush()
//   
//   $response1 = $reader.ReadLine()
//   Write-Host "Response 1: $response1"
//   
//   # Request 2 on SAME connection
//   $writer.WriteLine("GET /request2 HTTP/1.1")
//   $writer.WriteLine("Host: localhost")
//   $writer.WriteLine("")
//   $writer.Flush()
//   
//   $response2 = $reader.ReadLine()
//   Write-Host "Response 2: $response2"
//   
//   $socket.Close()
//   ```
//
// Expected Output:
//   Response 1: HTTP/1.1 200 OK
//   [Client] Keep-alive enabled, waiting for request #2...
//   Response 2: HTTP/1.1 200 OK
//   [Client] Disconnected from 127.0.0.1:xxxxx
//
// Debugging:
//   - Check ShouldKeepAlive() returns true for HTTP/1.1
//   - Check HandleClientAsync() loop continues when keepAlive = true
//   - Console logs should show "waiting for request #2"
//
//
// ============================================================================
// PHASE 2 TESTING: ROUTING & SECURITY
// ============================================================================
//
// TEST 2.1: GET /health Endpoint
// ───────────────────────────────
// Expected: Server has a /health route that returns 200 OK
//
// Command:
//   curl http://localhost:8080/health
//
// Expected Output:
//   Content-Type: application/json
//   {"status":"healthy"}
//
//
// TEST 2.2: GET /echo Endpoint
// ────────────────────────────
// Expected: Server echoes back request info
//
// Command:
//   curl "http://localhost:8080/echo?foo=bar" -H "X-Test-Header: value123"
//
// Expected Output:
//   Method: GET
//   Path: /echo?foo=bar
//   Headers: ...
//
//
// TEST 2.3: 404 Not Found
// ───────────────────────
// Expected: Non-existent path returns 404
//
// Command:
//   curl http://localhost:8080/nonexistent
//
// Expected Output:
//   HTTP/1.1 404 Not Found
//
//
// TEST 2.4: Path Traversal Attack (SECURITY)
// ─────────────────────────────────────────
// Expected: Server rejects or normalizes path traversal attempts
//
// Command:
//   curl "http://localhost:8080/../../../etc/passwd"
//   curl "http://localhost:8080/static/../../config.json"
//
// Expected Output:
//   Either 400 Bad Request or the path is normalized safely
//   NOT: Serving /etc/passwd or /config.json
//
// Debugging:
//   - Implement path normalization logic
//   - Reject paths that escape root (start with /..)
//   - Log suspicious paths for monitoring
//
//
// TEST 2.5: Request Smuggling Prevention (SECURITY)
// ──────────────────────────────────────────────────
// Expected: Server rejects ambiguous requests
//
// Commands:
//
// a) Duplicate Content-Length
//   ```
//   POST / HTTP/1.1\r\n
//   Host: localhost\r\n
//   Content-Length: 5\r\n
//   Content-Length: 10\r\n
//   \r\n
//   [body]
//   ```
//   Expected: 400 Bad Request
//
// b) Content-Length + Transfer-Encoding
//   ```
//   POST / HTTP/1.1\r\n
//   Host: localhost\r\n
//   Content-Length: 10\r\n
//   Transfer-Encoding: chunked\r\n
//   \r\n
//   [chunks]
//   ```
//   Expected: 400 Bad Request
//
// Debugging:
//   In ParseHttpRequestAsync(), when building headers:
//   - If Content-Length seen twice with different values → error
//   - If both Content-Length AND Transfer-Encoding → error
//
//
// ============================================================================
// PERFORMANCE TESTING
// ============================================================================
//
// TEST 3.1: Concurrent Connections
// ────────────────────────────────
// Expected: Server handles many simultaneous clients
//
// Command (PowerShell):
//   ```powershell
//   $tasks = @()
//   for ($i = 0; $i -lt 100; $i++) {
//       $tasks += [System.Threading.Tasks.Task]::Run({
//           (Invoke-WebRequest -Uri "http://localhost:8080/" -TimeoutSec 5).StatusCode
//       })
//   }
//   [System.Threading.Tasks.Task]::WaitAll($tasks)
//   Write-Host "All 100 requests completed"
//   ```
//
// Expected Output:
//   All 100 requests completed
//   Server console shows 100 client connects/disconnects
//
//
// TEST 3.2: Request Throughput
// ────────────────────────────
// Expected: Server handles requests/second without degradation
//
// Command (PowerShell):
//   ```powershell
//   $sw = [System.Diagnostics.Stopwatch]::StartNew()
//   for ($i = 0; $i -lt 1000; $i++) {
//       Invoke-WebRequest -Uri "http://localhost:8080/" -TimeoutSec 5 | Out-Null
//   }
//   $sw.Stop()
//   Write-Host "1000 requests in $($sw.ElapsedMilliseconds)ms"
//   Write-Host "Average: $( [math]::Round(1000000 / $sw.ElapsedMilliseconds, 2) ) req/sec"
//   ```
//
//
// ============================================================================
// DEBUGGING CHECKLIST
// ============================================================================
//
// When something breaks, check:
//
// ☐ Is the server listening on port 8080? 
//   Command: netstat -ano | findstr :8080
//
// ☐ Can you reach it? 
//   Command: ping 127.0.0.1
//
// ☐ Are you using the right HTTP method (GET vs POST)?
//
// ☐ Are headers formatted correctly? (Key: Value\r\n, case-insensitive)
//
// ☐ Did you include blank line after headers? (\r\n\r\n)
//
// ☐ Is timeout set correctly? (not too short, not allowing Slowloris)
//
// ☐ Check server console output for [Request], [Response], [Client] logs
//
// ☐ For timeout issues, ensure CancellationToken is connected to Task.Delay
//
// ☐ For keep-alive issues, verify ShouldKeepAlive() logic matches RFC 7230
//
//
// ============================================================================
