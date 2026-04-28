using System.Net;
using System.Net.Http;

namespace DeckFlow.Web.Tests;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    /// <summary>Immutable snapshot of a recorded HTTP request. Safe to read after the request is disposed.</summary>
    public record RecordedRequest(Uri? RequestUri, string Method);

    private readonly Queue<HttpResponseMessage> _responses = new();
    public IList<RecordedRequest> RecordedRequests { get; } = new List<RecordedRequest>();
    public int CallCount => RecordedRequests.Count;
    public Exception? NextException { get; set; }

    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RecordedRequests.Add(new RecordedRequest(request.RequestUri, request.Method.Method));

        if (NextException is not null)
        {
            var ex = NextException;
            NextException = null;
            throw ex;
        }

        var response = _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.NotFound);

        return Task.FromResult(response);
    }
}
