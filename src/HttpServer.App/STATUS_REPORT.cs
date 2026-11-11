/*
╔══════════════════════════════════════════════════════════════════════════════╗
║                                                                              ║
║               PROJECT 1: CUSTOM HTTP SERVER - STATUS REPORT                 ║
║                          (Targeting .NET 10)                                ║
║                                                                              ║
╚══════════════════════════════════════════════════════════════════════════════╝

═══════════════════════════════════════════════════════════════════════════════
CURRENT STATE
═══════════════════════════════════════════════════════════════════════════════

✓ COMPLETED:
  - Project structure created (HttpServer.App, HttpServer.Tests)
  - Core classes scaffolded: HttpRequest, HttpResponse, HttpServerImplementation
  - TCP listener foundation implemented
  - Connection handler with keep-alive loop structure
  - Security configuration constants (MaxHeaderSize, timeouts)
  - Comprehensive documentation with inline comments

⚠ TO-DO (Prioritized by Dependency):

  PRIORITY 1 - CRITICAL PATH (Foundation):
  ────────────────────────────────────────
  1. ParseHttpRequestAsync()
     └─ Reads HTTP request from network stream
     └─ Parses method, path, version, headers, body
     └─ Enforces security limits (header size, timeouts)
     └─ Detects smuggling attacks
     File: HttpServerImplementation.cs, line 140
     Status: Core function - blocks all other features

  2. SendHttpResponseAsync()
     └─ Formats and sends HTTP response to client
     └─ Ensures proper headers (Content-Length required for keep-alive)
     File: HttpServerImplementation.cs, line 230
     Status: Required for any working server

  3. ShouldKeepAlive()
     └─ Determines if connection stays open after response
     └─ Returns false on "Connection: close"
     └─ Difference between HTTP/1.0 and HTTP/1.1
     File: HttpServerImplementation.cs, line 260
     Status: Enables keep-alive feature


  PRIORITY 2 - TESTING & VALIDATION:
  ──────────────────────────────────
  4. Unit tests for ParseHttpRequestAsync()
     └─ Valid requests, header limits, timeouts, smuggling detection
     File: tests/HttpServer.Tests/UnitTest1.cs

  5. Integration tests
     └─ End-to-end tests with real HTTP client
     └─ Keep-alive verification
     └─ Timeout scenarios


  PRIORITY 3 - ROUTING & APPLICATION LOGIC:
  ──────────────────────────────────────────
  6. Routing system
     └─ Map paths to handlers (/health, /echo, etc.)
     └─ Replace static response in HandleRequest()

  7. Handler implementations
     └─ HealthHandler, EchoHandler, NotFoundHandler

  8. Path traversal protection
     └─ Normalize paths, prevent /../../../ attacks


  PRIORITY 4 - ADVANCED SECURITY:
  ─────────────────────────────
  9. Request smuggling detection
     └─ Catch duplicate Content-Length
     └─ Catch Content-Length + Transfer-Encoding

  10. Timeout hardening
      └─ Per-phase timeouts (request line, headers, body)
      └─ Slowloris mitigation

  11. Structured logging
      └─ Replace Console.WriteLine with proper logging


═══════════════════════════════════════════════════════════════════════════════
ARCHITECTURE OVERVIEW
═══════════════════════════════════════════════════════════════════════════════

         Client (curl, browser, etc.)
               ↓ TCP Connect
         ┌─────────────────────┐
         │  TcpListener        │  Listening on port 8080
         │  Accepts connection │
         └──────────┬──────────┘
                    ↓
         ┌─────────────────────────────────────────┐
         │ HandleClientAsync (Per-Connection Task) │
         │                                         │
         │  ┌─ Keep-Alive Loop (Request N) ─┐    │
         │  │                                │    │
         │  │  1. ParseHttpRequestAsync()   │    │
         │  │     ↓ HttpRequest             │    │
         │  │  2. HandleRequest()           │    │
         │  │     ↓ HttpResponse            │    │
         │  │  3. SendHttpResponseAsync()   │    │
         │  │                                │    │
         │  │  4. ShouldKeepAlive()?        │    │
         │  │     YES → Loop back to step 1 │    │
         │  │     NO  → Close connection     │    │
         │  └────────────────────────────────┘    │
         │                                        │
         └────────────────────────────────────────┘


═══════════════════════════════════════════════════════════════════════════════
SECURITY FEATURES (Core Concepts with Defenses)
═══════════════════════════════════════════════════════════════════════════════

[1] SLOWLORIS ATTACK
   ├─ What: Attacker sends HTTP headers very slowly, exhausting server connections
   ├─ Surface: RequestTimeoutMs = 30 seconds per request
   ├─ Defense: 
   │  ✓ Timeout on stream.ReadAsync() via CancellationToken
   │  ✓ Connection times out if no complete request arrives
   │  ✓ Server disconnects attacker after timeout
   └─ Test: TESTING_GUIDE.cs → TEST 1.5

[2] REQUEST SMUGGLING
   ├─ What: Attacker sends ambiguous request (CL + TE) that's parsed differently
   │         by front-end proxy and back-end server → desynchronization
   ├─ Attack Vector A:
   │  - Request has "Content-Length: 10" AND "Content-Length: 20"
   │  - Front-end uses 10 bytes, back-end uses 20 bytes
   │  - Bytes 11-20 are treated as next request (smuggling)
   ├─ Attack Vector B:
   │  - Request has "Content-Length: 10" AND "Transfer-Encoding: chunked"
   │  - Front-end uses Content-Length, back-end uses chunked
   │  - Attacker injects commands after chunk
   ├─ Defense:
   │  ✓ In ParseHttpRequestAsync(), check for duplicate Content-Length
   │  ✓ Reject if CL value differs
   │  ✓ Reject if BOTH Content-Length AND Transfer-Encoding present
   │  ✓ Return 400 Bad Request for ambiguous requests
   └─ Test: TESTING_GUIDE.cs → TEST 2.5

[3] HEADER SIZE LIMITS (Memory DoS)
   ├─ What: Attacker sends 1 GB+ of headers, server runs out of memory
   ├─ Defense:
   │  ✓ MaxHeaderSize = 8 KB constant
   │  ✓ In ParseHttpRequestAsync(), accumulate header size
   │  ✓ Reject if total > MaxHeaderSize → 413 Payload Too Large
   └─ Test: TESTING_GUIDE.cs → TEST 1.4

[4] PATH TRAVERSAL
   ├─ What: Attacker requests /static/../../../../../../etc/passwd
   ├─ Goal: Access files outside intended directory
   ├─ Defense:
   │  ✓ Normalize path (collapse .., remove //, etc.)
   │  ✓ Ensure final path doesn't escape root (no leading /..)
   │  ✓ Validate before any file access
   └─ Test: TESTING_GUIDE.cs → TEST 2.4

[5] PROTOCOL VIOLATION
   ├─ What: Malformed HTTP (invalid method, version, headers)
   ├─ Defense:
   │  ✓ Validate HTTP method (GET, POST, PUT, DELETE, etc.)
   │  ✓ Validate HTTP version (1.0, 1.1)
   │  ✓ Reject on parse errors → 400 Bad Request
   └─ Implementation: ParseHttpRequestAsync()


═══════════════════════════════════════════════════════════════════════════════
QUICK START GUIDE
═══════════════════════════════════════════════════════════════════════════════

Step 1: Implement ParseHttpRequestAsync()
   Location: src/HttpServer.App/HttpServerImplementation.cs, line 140

   Pseudocode:
   ```
   1. Read request line (with timeout via CancellationToken)
      Format: "GET / HTTP/1.1\r\n"
      → Split into [method, path, version]
      → Validate each

   2. Read headers until blank line
      Repeat:
         a. Read line
         b. If line == "" (blank), break
         c. Parse "HeaderName: HeaderValue"
         d. Accumulate size; reject if > MaxHeaderSize

   3. Read body
      If request has "Content-Length: N":
         → Read exactly N bytes
      If request has "Transfer-Encoding: chunked":
         → Decode chunks
      Else:
         → No body

   4. Return new HttpRequest { Method, Path, HttpVersion, Headers, Body }
   ```


Step 2: Test Parsing
   In Terminal:
   ```
   cd C:\Users\AGUDR\HttpServer
   dotnet run --project src/HttpServer.App/HttpServer.App.csproj
   ```

   In another PowerShell:
   ```
   curl http://localhost:8080/
   ```

   Expected: Server prints "[Request] GET / HTTP/1.1", responds with 200 OK


Step 3: Implement SendHttpResponseAsync()
   Location: src/HttpServer.App/HttpServerImplementation.cs, line 230

   Just format the response and write to stream:
   ```
   HTTP/1.1 200 OK\r\n
   Content-Type: text/plain\r\n
   Content-Length: 32\r\n
   \r\n
   Hello from custom HTTP server!
   ```


Step 4: Run Tests
   ```
   dotnet test tests/HttpServer.Tests/HttpServer.Tests.csproj
   ```


═══════════════════════════════════════════════════════════════════════════════
FILES & PURPOSE
═══════════════════════════════════════════════════════════════════════════════

src/HttpServer.App/
├─ Program.cs
│  └─ Entry point, creates HttpServerImplementation, starts listening
│
├─ HttpServerImplementation.cs ⭐ MAIN FILE
│  └─ Core server logic:
│     ├─ HttpServerImplementation class (TCP listener)
│     ├─ HandleClientAsync (per-connection handler)
│     ├─ ParseHttpRequestAsync() [TODO]
│     ├─ SendHttpResponseAsync() [TODO]
│     ├─ HandleRequest() [TODO: Add routing]
│     ├─ ShouldKeepAlive() [TODO]
│     ├─ HttpRequest class
│     └─ HttpResponse class
│
├─ PROGRESS_ROADMAP.cs
│  └─ Detailed breakdown of each TODO with implementation tips
│
└─ TESTING_GUIDE.cs
   └─ Practical commands to test each feature

tests/HttpServer.Tests/
└─ UnitTest1.cs [TODO: Add tests]


═══════════════════════════════════════════════════════════════════════════════
CONSTANTS & TUNING
═══════════════════════════════════════════════════════════════════════════════

const int MaxHeaderSize = 8192;         // 8 KB - header size limit
const int RequestTimeoutMs = 30000;     // 30 seconds per request
const int KeepAliveTimeoutMs = 5000;    // 5 seconds waiting for next request

Note: These are conservative values suitable for public-facing servers.
For internal services, you might increase them. For high-security scenarios,
consider tightening.


═══════════════════════════════════════════════════════════════════════════════
NEXT ACTIONS (In Order)
═══════════════════════════════════════════════════════════════════════════════

1. Read PROGRESS_ROADMAP.cs - understand what needs implementation
2. Read TESTING_GUIDE.cs - know how to validate your work
3. Implement ParseHttpRequestAsync() with comments from PROGRESS_ROADMAP.cs
4. Test with: curl http://localhost:8080/
5. Implement SendHttpResponseAsync()
6. Implement ShouldKeepAlive()
7. Run tests for keep-alive: send 2 requests on same connection
8. Add routing (reference PROGRESS_ROADMAP.cs step 4)
9. Add path traversal protection
10. Write unit tests (reference PROGRESS_ROADMAP.cs step 9-10)

═══════════════════════════════════════════════════════════════════════════════
*/
