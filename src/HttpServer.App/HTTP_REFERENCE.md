// ============================================================================
// HTTP REQUEST/RESPONSE REFERENCE FOR CUSTOM HTTP SERVER
// ============================================================================
//
// This file contains HTTP protocol details needed for implementation.
// Reference: RFC 7230 (HTTP/1.1 Message Syntax and Routing)
//
// ============================================================================
// HTTP/1.1 REQUEST FORMAT
// ============================================================================
//
// Structure:
//   REQUEST-LINE\r\n
//   HEADER-LINE\r\n
//   HEADER-LINE\r\n
//   ... (more headers)
//   \r\n  (blank line)
//   [BODY - optional]
//
// Example request with headers:
//
//   GET /api/users HTTP/1.1\r\n
//   Host: example.com\r\n
//   User-Agent: curl/7.64.1\r\n
//   Accept: */*\r\n
//   \r\n
//
// Example request with body (POST):
//
//   POST /api/users HTTP/1.1\r\n
//   Host: example.com\r\n
//   Content-Type: application/json\r\n
//   Content-Length: 27\r\n
//   \r\n
//   {"name":"John","age":30}
//
// Key Points:
// - METHOD: GET, POST, PUT, DELETE, HEAD, OPTIONS, PATCH, TRACE, CONNECT
// - URI: Path (e.g., /path/to/resource)
// - VERSION: HTTP/1.0, HTTP/1.1, HTTP/2 (we're implementing 1.1)
// - HEADERS: Key: Value (case-insensitive key, trimmed value)
// - BLANK-LINE: Mark end of headers (just \r\n)
// - BODY: Optional. Size determined by Content-Length header
//
//
// ============================================================================
// HTTP/1.1 RESPONSE FORMAT
// ============================================================================
//
// Structure:
//   STATUS-LINE\r\n
//   HEADER-LINE\r\n
//   HEADER-LINE\r\n
//   ... (more headers)
//   \r\n  (blank line)
//   [BODY - optional]
//
// Example response (200 OK):
//
//   HTTP/1.1 200 OK\r\n
//   Content-Type: application/json\r\n
//   Content-Length: 18\r\n
//   Connection: keep-alive\r\n
//   \r\n
//   {"id":123,"name":"John"}
//
// Example response (404 Not Found):
//
//   HTTP/1.1 404 Not Found\r\n
//   Content-Type: text/plain\r\n
//   Content-Length: 9\r\n
//   \r\n
//   Not Found
//
// Status Codes:
// - 1xx: Informational (100 Continue, 101 Switching Protocols)
// - 2xx: Success (200 OK, 201 Created, 204 No Content)
// - 3xx: Redirection (301 Moved Permanently, 302 Found, 304 Not Modified)
// - 4xx: Client Error (400 Bad Request, 401 Unauthorized, 403 Forbidden,
//                      404 Not Found, 405 Method Not Allowed, 413 Payload Too Large)
// - 5xx: Server Error (500 Internal Server Error, 501 Not Implemented,
//                      503 Service Unavailable)
//
// Important: For keep-alive to work, Content-Length MUST be set in response
// (unless using Transfer-Encoding: chunked or Connection: close)
//
//
// ============================================================================
// KEEP-ALIVE FLOW
// ============================================================================
//
// With keep-alive (HTTP/1.1 default):
//
//   Single TCP Connection (stays open for multiple requests)
//   Client -> Server: Request #1
//   Server -> Client: Response #1
//   Client -> Server: Request #2  (same connection!)
//   Server -> Client: Response #2
//   Client -> Server: Request #3  (same connection!)
//   Server -> Client: Response #3
//   [connection closes or timeout occurs]
//
// Without keep-alive:
//
//   Connection #1 for Request #1 / Response #1
//   Close connection
//   Connection #2 for Request #2 / Response #2
//   Close connection
//   Connection #3 for Request #3 / Response #3
//   Close connection
//
// HTTP/1.1 defaults to keep-alive UNLESS:
//   - Request has "Connection: close" header
//   - Response has "Connection: close" header
//   - Content-Length not set (forces close)
//
// HTTP/1.0 defaults to close UNLESS:
//   - Request has "Connection: keep-alive" header
//
//
// ============================================================================
// CRITICAL HEADERS FOR THIS PROJECT
// ============================================================================
//
// REQUEST HEADERS:
//
//   Host: example.com
//     - MANDATORY in HTTP/1.1 (RFC 7230)
//     - Identify target server
//
//   Content-Length: <number>
//     - Specifies exactly how many bytes in request body
//     - Example: "Content-Length: 27" means read 27 bytes
//     - Can appear at most once
//
//   Transfer-Encoding: chunked
//     - Alternative to Content-Length
//     - Body is sent in chunks with size prefix
//     - Format: "5\r\nhello\r\nA\r\nmore data\r\n0\r\n\r\n"
//     - CANNOT appear with Content-Length (request smuggling!)
//
//   Connection: close
//     - Explicitly request connection close after response
//     - HTTP/1.1 default is keep-alive, so this overrides it
//
//   Connection: keep-alive
//     - Explicitly request keep-alive (redundant in HTTP/1.1)
//     - Important for HTTP/1.0 clients
//
//   User-Agent: <string>
//     - Client identifier (informational only)
//
//   Accept: <content-type>
//     - Preferred response content type (informational)
//
// RESPONSE HEADERS:
//
//   Content-Length: <number>
//     - Specifies exactly how many bytes in response body
//     - REQUIRED for keep-alive (unless chunked or close)
//     - Without it, client doesn't know when body ends
//
//   Content-Type: <media-type>
//     - Response body type (e.g., application/json, text/html, text/plain)
//
//   Connection: close
//     - Server closing connection after response
//     - Client must not expect keep-alive
//
//   Connection: keep-alive
//     - Server confirming keep-alive (optional in HTTP/1.1)
//
//   Server: <name>
//     - Server identification (informational)
//
//   Date: <RFC7231 date>
//     - Server timestamp
//
//
// ============================================================================
// REQUEST SMUGGLING ATTACKS & DETECTION
// ============================================================================
//
// Attack Vector 1: Duplicate Content-Length
//   POST / HTTP/1.1\r\n
//   Host: localhost\r\n
//   Content-Length: 10\r\n
//   Content-Length: 20\r\n
//   \r\n
//   [10 bytes for req 1][10 bytes smuggled as req 2]
//
//   Defense: REJECT if multiple Content-Length headers with different values
//
//
// Attack Vector 2: Content-Length + Transfer-Encoding
//   POST / HTTP/1.1\r\n
//   Host: localhost\r\n
//   Content-Length: 10\r\n
//   Transfer-Encoding: chunked\r\n
//   \r\n
//   [10 bytes for req 1][chunked request 2]
//
//   Problem: Front-end proxy uses Content-Length, back-end uses chunked
//            Result: Desynchronization between proxy and server
//
//   Defense: REJECT if BOTH Content-Length and Transfer-Encoding present
//
//
// ============================================================================
// PARSING PSEUDOCODE
// ============================================================================
//
// function ParseHttpRequest(stream, timeout):
//   
//   // STEP 1: Read request line
//   line = ReadLineUntilCRLF(stream, timeout=5s)
//   parts = line.Split(' ')
//   method = parts[0]        // "GET", "POST", etc.
//   path = parts[1]          // "/path"
//   version = parts[2]       // "HTTP/1.1"
//   
//   ValidateMethod(method)
//   ValidateVersion(version)
//   
//   
//   // STEP 2: Read headers
//   headers = CaseInsensitiveDictionary()
//   totalSize = 0
//   
//   loop:
//     line = ReadLineUntilCRLF(stream, timeout=5s)
//     
//     if line is empty:       // blank line marks end of headers
//       break
//     
//     if totalSize + line.length > MaxHeaderSize:
//       throw PayloadTooLargeError
//     
//     totalSize += line.length
//     
//     key, value = line.Split(":", 1)
//     key = key.Trim().ToLower()
//     value = value.Trim()
//     
//     // SMUGGLING DETECTION
//     if key == "content-length":
//       if key in headers and headers[key] != value:
//         throw BadRequestError("Duplicate Content-Length with different value")
//       if "transfer-encoding" in headers:
//         throw BadRequestError("Cannot have both Content-Length and Transfer-Encoding")
//     
//     if key == "transfer-encoding":
//       if "content-length" in headers:
//         throw BadRequestError("Cannot have both Content-Length and Transfer-Encoding")
//     
//     headers[key] = value
//   
//   
//   // STEP 3: Read body
//   body = null
//   
//   if "content-length" in headers:
//     length = int.Parse(headers["content-length"])
//     if length < 0:
//       throw BadRequestError("Negative Content-Length")
//     body = ReadExactBytes(stream, length)
//   
//   else if "transfer-encoding" in headers:
//     if "chunked" in headers["transfer-encoding"].lower():
//       body = ReadChunkedBody(stream)
//   
//   return HttpRequest { method, path, version, headers, body }
//
//
// ============================================================================
// IMPLEMENTATION TIPS
// ============================================================================
//
// Reading a line ("\r\n" terminated):
//   - Can't use StreamReader.ReadLine() directly (may not have \r\n)
//   - Instead: read bytes, accumulate until you see \r\n
//   - Strip the \r\n before processing
//   - Enforce timeout via CancellationToken + Task.Delay
//
// Enforcing timeout:
//   readTask = stream.ReadAsync(buffer, ...)
//   timeoutTask = Task.Delay(5000)
//   finished = await Task.WhenAny(readTask, timeoutTask)
//   if (finished == timeoutTask) throw TimeoutException()
//   // Process readTask.Result
//
// Case-insensitive headers:
//   Use Dictionary<string, string> with header names converted to lower case
//   When looking up: header.ToLower()
//   When returning: preserve original casing if needed
//
// Content-Length parsing:
//   value = headers["content-length"]  // "27"
//   length = int.Parse(value)           // 27
//   bytes = await stream.ReadAsync(..., length) // read 27 bytes
//
// Chunked encoding:
//   Format: "5\r\nhello\r\n10\r\n0123456789ABCDEF\r\n0\r\n\r\n"
//   - "5" = chunk size (hex or decimal)
//   - "\r\n"
//   - [5 bytes data]
//   - "\r\n"
//   - Repeat until chunk size 0
//   - Last "0\r\n\r\n" marks end
//
// ============================================================================
