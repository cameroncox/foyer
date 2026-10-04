using System.Net;

namespace Foyer.Core.Tests.Support;

/// <summary>Answers requests from a URL → response map and records what was asked for.</summary>
public sealed class FakeHttp : HttpMessageHandler, IHttpClientFactory
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = [];
    private readonly List<string> _requests = [];

    public IReadOnlyList<string> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    public FakeHttp Respond(string url, byte[] body, HttpStatusCode status = HttpStatusCode.OK, string contentType = "application/octet-stream")
    {
        _responses[url] = () => new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(body) { Headers = { { "Content-Type", contentType } } },
        };
        return this;
    }

    public FakeHttp Fail(string url)
    {
        _responses[url] = () => throw new HttpRequestException("Connection refused");
        return this;
    }

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.AbsoluteUri;
        lock (_requests)
        {
            _requests.Add(url);
        }

        return Task.FromResult(_responses.TryGetValue(url, out var respond)
            ? respond()
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
