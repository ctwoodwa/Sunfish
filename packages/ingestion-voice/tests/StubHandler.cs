namespace Sunfish.Ingestion.Voice.Tests;

/// <summary>
/// Test-only <see cref="HttpMessageHandler"/> that delegates to a caller-supplied function.
/// Keeps unit tests free of network I/O.
/// </summary>
internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        => Task.FromResult(respond(req));
}
