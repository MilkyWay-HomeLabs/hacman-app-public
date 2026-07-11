using System.Net;
using System.Text;

namespace hacman_app_front.Tests.TestHelpers;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/>: returns canned responses and records
/// every request (method, URL, body) so tests can assert on what the service actually sent.
/// Request bodies are read eagerly at send time, before the request content is disposed.
/// </summary>
public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string?> RequestBodies { get; } = [];

    /// <summary>Handler that answers every request with the given status and optional JSON body.</summary>
    public static FakeHttpMessageHandler Returning(HttpStatusCode statusCode, string? jsonBody = null)
        => new(_ => BuildResponse(statusCode, jsonBody));

    public static HttpResponseMessage BuildResponse(HttpStatusCode statusCode, string? jsonBody = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (jsonBody != null)
        {
            response.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        return response;
    }

    /// <summary>Wraps this handler in an <see cref="HttpClient"/> with a fixed test base address.</summary>
    public HttpClient CreateClient(string baseAddress = "https://api.test.local/")
        => new(this) { BaseAddress = new Uri(baseAddress) };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content == null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken));

        return responder(request);
    }
}