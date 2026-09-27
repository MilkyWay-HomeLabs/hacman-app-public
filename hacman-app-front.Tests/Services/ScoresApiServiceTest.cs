using System.Net;
using hacman_app_front.Services;
using hacman_app_front.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace hacman_app_front.Tests.Services;

public class ScoresApiServiceTest
{
    private static ScoresApiService CreateService(FakeHttpMessageHandler handler, string? accessToken = null)
        => new(handler.CreateClient(), NullLogger<ScoresApiService>.Instance,
            new UserSessionState { AccessToken = accessToken });

    private static string ScoreJson(string username, long timeMs)
        => $$"""{"username":"{{username}}","timeMs":{{timeMs}},"achievedAt":"2026-01-01T00:00:00Z","difficulty":""}""";

    /// <summary>Responds per difficulty query param; unlisted difficulties get an empty array.</summary>
    private static FakeHttpMessageHandler HandlerWithScoresPerDifficulty(
        Dictionary<string, string> jsonArrayPerDifficulty,
        HttpStatusCode failingStatus = HttpStatusCode.InternalServerError,
        params string[] failingDifficulties)
        => new(request =>
        {
            var url = request.RequestUri!.ToString();
            if (failingDifficulties.Any(difficulty => url.Contains($"difficulty={difficulty}")))
            {
                return FakeHttpMessageHandler.BuildResponse(failingStatus);
            }

            foreach (var (difficulty, json) in jsonArrayPerDifficulty)
            {
                if (url.Contains($"difficulty={difficulty}"))
                {
                    return FakeHttpMessageHandler.BuildResponse(HttpStatusCode.OK, json);
                }
            }

            return FakeHttpMessageHandler.BuildResponse(HttpStatusCode.OK, "[]");
        });

    [Fact]
    public async Task GetScores_FetchesAllThreeDifficulties_AndAggregatesResults()
    {
        var handler = HandlerWithScoresPerDifficulty(new Dictionary<string, string>
        {
            ["EASY"] = $"[{ScoreJson("easy-player", 1000)}]",
            ["MEDIUM"] = $"[{ScoreJson("medium-player", 2000)}]",
            ["HARD"] = $"[{ScoreJson("hard-player-1", 3000)},{ScoreJson("hard-player-2", 4000)}]"
        });
        var service = CreateService(handler);

        var scores = await service.GetScores(Guid.NewGuid());

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(4, scores.Count);
        Assert.Contains(scores, s => s.Username == "easy-player");
        Assert.Contains(scores, s => s.Username == "hard-player-2");
    }

    [Fact]
    public async Task GetScores_RequestsExpectedUrlPerDifficulty()
    {
        var galleryId = Guid.NewGuid();
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "[]");
        var service = CreateService(handler);

        await service.GetScores(galleryId);

        var urls = handler.Requests.Select(r => r.RequestUri!.ToString()).ToList();
        Assert.Equal(3, urls.Count);
        foreach (var difficulty in new[] { "EASY", "MEDIUM", "HARD" })
        {
            Assert.Contains(urls, u =>
                u ==
                $"https://api.test.local/v1/level-scores?galleryId={galleryId}&difficulty={difficulty}&pageNumber=1&pageSize=10");
        }
    }

    [Fact]
    public async Task GetScores_TagsEachScoreWithItsRequestDifficulty()
    {
        // The API response carries an empty difficulty; the service must stamp the one it queried for.
        var handler = HandlerWithScoresPerDifficulty(new Dictionary<string, string>
        {
            ["EASY"] = $"[{ScoreJson("easy-player", 1000)}]",
            ["HARD"] = $"[{ScoreJson("hard-player", 3000)}]"
        });
        var service = CreateService(handler);

        var scores = await service.GetScores(Guid.NewGuid());

        Assert.Equal("EASY", scores.Single(s => s.Username == "easy-player").Difficulty);
        Assert.Equal("HARD", scores.Single(s => s.Username == "hard-player").Difficulty);
    }

    [Fact]
    public async Task GetScores_ContinuesWithOtherDifficulties_WhenOneFails()
    {
        var handler = HandlerWithScoresPerDifficulty(
            new Dictionary<string, string>
            {
                ["EASY"] = $"[{ScoreJson("easy-player", 1000)}]",
                ["HARD"] = $"[{ScoreJson("hard-player", 3000)}]"
            },
            failingDifficulties: "MEDIUM");
        var service = CreateService(handler);

        var scores = await service.GetScores(Guid.NewGuid());

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(2, scores.Count);
        Assert.DoesNotContain(scores, s => s.Difficulty == "MEDIUM");
    }

    [Fact]
    public async Task GetScores_ReturnsEmptyList_WhenAllDifficultiesFail()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.InternalServerError);
        var service = CreateService(handler);

        var scores = await service.GetScores(Guid.NewGuid());

        Assert.Empty(scores);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task GetScores_SetsBearerAuthorizationHeader_WhenTokenIsAvailable()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "[]");
        var service = CreateService(handler, accessToken: "test-token-123");

        await service.GetScores(Guid.NewGuid());

        Assert.All(handler.Requests, request =>
        {
            Assert.NotNull(request.Headers.Authorization);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-token-123", request.Headers.Authorization.Parameter);
        });
    }

    [Fact]
    public async Task GetScores_SendsNoAuthorizationHeader_WhenTokenIsMissing()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "[]");
        var service = CreateService(handler, accessToken: null);

        await service.GetScores(Guid.NewGuid());

        Assert.All(handler.Requests, request => Assert.Null(request.Headers.Authorization));
    }
}