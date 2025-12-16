# HttpClientTestServer
Test HTTP Server for YetAnotherHttpHandler

## Usage
### Using Docker

```bash
docker run --rm -it -p 8080:80 ghcr.io/cysharp/httpclienttestserver:main --port 80 --protocol Http1AndHttp2 --secure --tls Tls13
```

### Using C#
Reference the HttpClientTestServer.Launcher project and call the `{InProcess,Container}TestServer.LaunchAsync` method.

```csharp
await using var server = await InProcessTestServer.LaunchAsync(
    TestServerListenMode.SecureHttp1AndHttp2,
    new TestOutputLoggerProvider(TestOutputHelper),
    TimeoutToken,
    new TestServerOptions(SslProtocols.Tls13)
);
```

## Endpoint
The server exposes the following endpoints:

- `/`
  - Returns `__OK__` as the response body.
- `/not-found`
  - Returns `__Not_Found__` with status code `404 Not Found`.
- `/response-headers`
    - Sets response header `x-test: foo` and returns `__OK__`.
- `/slow-response-headers`
    - Waits 1 second, then sets response header `x-test: foo` and returns `__OK__`. If the request is aborted, marks `SessionStateFeature.Items["IsCanceled"] = true`.
- `/ハロー`
    - Returns `Konnichiwa` (Japanese path).
- `/slow-upload` (POST)
    - Slowly reads and discards the entire request body (1-second delay per read) and then returns `OK`.
- `/post-echo` (POST)
    - Reads the entire request body and returns it as `application/octet-stream`. Also echoes the request `Content-Type` into response header `x-request-content-type`.
- `/post-streaming` (POST)
    - Immediately sends status code `200 OK` and header `x-header-1: foo`, streams and counts all incoming request bytes, then writes the total byte length as the response body and returns an empty result.
- `/post-response-trailers` (POST)
  - Appends response trailers `x-trailer-1: foo` and `x-trailer-2: bar`, then returns `__OK__` with status `200 OK`.
- `/post-response-headers-immediately` (POST)
  - Immediately sends status code `202 Accepted` and header `x-header-1: foo`, waits 100 seconds, then writes `__OK__` and returns an empty result.
- `/post-abort-while-reading` (POST)
  - Reads once from the request body, then aborts the HTTP context, terminating the connection, and returns an empty result.
- `/post-null` (POST)
  - Continuously reads and discards the request body until it is completed, canceled, or the request is aborted, then returns an empty result.
- `/post-null-duplex` (POST)
  - Immediately sends status code `200 OK` and header `x-header-1: foo`, then continues to read and discard the request body until it is completed, canceled, or the request is aborted, and returns an empty result.
- `/post-never-read` (POST)
  - Immediately sends status code `200 OK` and header `x-header-1: foo`, never reads the request body, and loops until the request is aborted, then returns an empty result.
- `/random` (GET, query `size`)
  - Generates `size` random bytes using `Random.Shared` and returns them as `application/octet-stream`.
- `/error-reset` (GET, HTTP/2 only)
  - Uses `IHttpResetFeature` to send an HTTP/2 stream reset with error code `2` (`INTERNAL_ERROR`) and then returns an empty result.

## Certificates
The server uses a self-signed certificate. You can obtain this certificate from the repository or by accessing `/_certs/localhost.{crt,key,pfx}`.

## License

MIT License