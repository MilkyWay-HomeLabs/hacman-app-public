using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace hacman_app_back.Tests.TestHelpers;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/>: returns canned responses and records
/// every request (method, URL, body, headers) so tests can assert on what the service
/// actually sent. Mirrors hacman-app-front.Tests/TestHelpers/FakeHttpMessageHandler.cs
/// (CLAUDE.md §5) — the Nebula adapter must be tested against a fake handler, never the
/// real nebula-rest-api.
/// </summary>
public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string?> RequestBodies { get; } = [];

    public static FakeHttpMessageHandler Returning(HttpStatusCode statusCode, string? jsonBody = null)
        => new(_ => BuildResponse(statusCode, jsonBody));

    public static HttpResponseMessage BuildResponse(HttpStatusCode statusCode, string? jsonBody = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (jsonBody != null)
            response.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        return response;
    }

    public HttpClient CreateClient(string baseAddress = "https://nebula.test.local/")
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
