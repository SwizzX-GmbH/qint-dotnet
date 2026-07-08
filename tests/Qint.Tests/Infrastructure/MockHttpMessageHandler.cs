using System.Net;

namespace Qint.Tests.Infrastructure;

/// <summary>
/// A test double for <see cref="HttpMessageHandler"/> that captures the outgoing
/// request (and its body) and returns a caller-supplied response. Runs fully offline.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _responder;

    public MockHttpMessageHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    /// <summary>Convenience constructor: always reply with <paramref name="statusCode"/> and <paramref name="json"/>.</summary>
    public MockHttpMessageHandler(HttpStatusCode statusCode, string json, string contentType = "application/json")
        : this((_, _) => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, contentType),
        })
    {
    }

    /// <summary>The most recent request the client sent.</summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>The most recent request body the client sent (null for bodyless requests).</summary>
    public string? LastRequestBody { get; private set; }

    /// <summary>Number of requests handled.</summary>
    public int CallCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return _responder(request, LastRequestBody);
    }
}
