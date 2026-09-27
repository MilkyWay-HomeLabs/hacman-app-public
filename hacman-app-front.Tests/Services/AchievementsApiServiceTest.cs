using System.Net;
using hacman_app_front.Services;
using hacman_app_front.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace hacman_app_front.Tests.Services;

public class AchievementsApiServiceTest
{
    private static AchievementsApiService CreateService(FakeHttpMessageHandler handler)
        => new(handler.CreateClient(), NullLogger<AchievementsApiService>.Instance);

    private const string SampleJson = """
        [
          {"code":"COL-01","category":"COLLECTION","name":"Archivist","description":"desc","iconUrl":null,"thresholds":[10,25,50,75,100],"value":30,"level":2,"nextThreshold":50},
          {"code":"VOL-01","category":"VOLUME","name":"Dedicated","description":"desc","iconUrl":null,"thresholds":[10,50,150,400,1000],"value":1000,"level":5,"nextThreshold":null}
        ]
        """;

    [Fact]
    public async Task GetMyProgress_RequestsExpectedUrl()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "[]");
        var service = CreateService(handler);

        await service.GetMyProgress();

        Assert.Single(handler.Requests);
        Assert.Equal("https://api.test.local/v1/achievements/me", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetMyProgress_DeserializesEveryField()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, SampleJson);
        var service = CreateService(handler);

        var result = await service.GetMyProgress();

        Assert.Equal(2, result.Count);

        var maxed = result.Single(a => a.Code == "VOL-01");
        Assert.Equal("VOLUME", maxed.Category);
        Assert.Equal("Dedicated", maxed.Name);
        Assert.Equal(1000L, maxed.Value);
        Assert.Equal(5, maxed.Level);
        Assert.Null(maxed.NextThreshold);

        var partial = result.Single(a => a.Code == "COL-01");
        Assert.Equal(30L, partial.Value);
        Assert.Equal(2, partial.Level);
        Assert.Equal(50, partial.NextThreshold);
        Assert.Equal(new[] { 10, 25, 50, 75, 100 }, partial.Thresholds);
    }

    [Fact]
    public async Task GetMyProgress_ReturnsEmptyList_WhenRequestFails()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.InternalServerError);
        var service = CreateService(handler);

        var result = await service.GetMyProgress();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMyProgress_ReturnsEmptyList_WhenUnauthorized()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.Unauthorized);
        var service = CreateService(handler);

        var result = await service.GetMyProgress();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMyProgress_ReturnsEmptyList_WhenTheHttpCallThrows()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var service = CreateService(handler);

        var result = await service.GetMyProgress();

        Assert.Empty(result);
    }
}
