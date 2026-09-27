#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using hacman_app_back.Modules.Achievements.Domain;
using hacman_app_back.Modules.Achievements.Infrastructure.Nebula;
using hacman_app_back.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace hacman_app_back.Tests.Modules.Achievements.Infrastructure.Nebula;

public class NebulaAchievementClientTest
{
    private static AchievementDef BuildDef(string code = "COL-01", string? iconUrl = null) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Category = AchievementCategory.COLLECTION,
        Name = "Archivist",
        Description = "% of the archive unlocked.",
        IconUrl = iconUrl,
        Metric = "galleries_unlocked_pct",
        Thresholds = new[] { 10, 25, 50, 75, 100 }
    };

    // --- Not-configured no-ops -------------------------------------------------------

    [Fact]
    public async Task SyncCatalogAsync_NoOps_WhenBaseAddressIsNotSet()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler); // no BaseAddress set
        var client = new NebulaAchievementClient(httpClient, "achv_token", NullLoggerOf<NebulaAchievementClient>());

        await client.SyncCatalogAsync(new List<AchievementDef> { BuildDef() });

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SyncCatalogAsync_NoOps_WhenTokenIsMissing()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK);
        var httpClient = handler.CreateClient();
        var client = new NebulaAchievementClient(httpClient, null, NullLoggerOf<NebulaAchievementClient>());

        await client.SyncCatalogAsync(new List<AchievementDef> { BuildDef() });

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SyncCatalogAsync_NoOps_WhenCatalogIsEmpty()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK);
        var httpClient = handler.CreateClient();
        var client = new NebulaAchievementClient(httpClient, "achv_token", NullLoggerOf<NebulaAchievementClient>());

        await client.SyncCatalogAsync(new List<AchievementDef>());

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ReportProgressAsync_NoOps_WhenNotConfigured()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var client = new NebulaAchievementClient(httpClient, "achv_token", NullLoggerOf<NebulaAchievementClient>());

        await client.ReportProgressAsync(Guid.NewGuid(), BuildDef(), 5, 1);

        Assert.Empty(handler.Requests);
    }

    // --- Catalog sync ------------------------------------------------------------------

    [Fact]
    public async Task SyncCatalogAsync_PostsToCatalogEndpoint_WithBearerAuthAndCorrectBody()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");
        var httpClient = handler.CreateClient();
        var client = new NebulaAchievementClient(httpClient, "achv_secret", NullLoggerOf<NebulaAchievementClient>());
        var def = BuildDef("COL-01", "https://milkyway.test/resources/hacman/icons/col-01.png");

        await client.SyncCatalogAsync(new List<AchievementDef> { def });

        Assert.Single(handler.Requests);
        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/achievements/catalog", request.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("achv_secret", request.Headers.Authorization!.Parameter);

        using var body = JsonDocument.Parse(handler.RequestBodies[0]!);
        var achievements = body.RootElement.GetProperty("achievements");
        Assert.Equal(1, achievements.GetArrayLength());

        var entry = achievements[0];
        Assert.Equal("COL-01", entry.GetProperty("externalId").GetString());
        Assert.Equal("Archivist", entry.GetProperty("name").GetString());
        Assert.Equal("https://milkyway.test/resources/hacman/icons/col-01.png",
            entry.GetProperty("iconUrl").GetString());
        Assert.Equal(0, entry.GetProperty("minValue").GetInt32());
        Assert.Equal(100, entry.GetProperty("maxValue").GetInt32());

        var levels = entry.GetProperty("levels");
        Assert.Equal(5, levels.GetArrayLength());
        Assert.Equal(1, levels[0].GetProperty("level").GetInt32());
        Assert.Equal(10, levels[0].GetProperty("value").GetInt32());
        Assert.Equal(5, levels[4].GetProperty("level").GetInt32());
        Assert.Equal(100, levels[4].GetProperty("value").GetInt32());

        // "source" must never be sent — Nebula derives it from the bearer token (CLAUDE.md §4.2).
        Assert.False(entry.TryGetProperty("source", out _));
        Assert.False(body.RootElement.TryGetProperty("source", out _));
    }

    [Fact]
    public async Task SyncCatalogAsync_DoesNotThrow_WhenNebulaReturnsAnErrorStatus()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.InternalServerError);
        var httpClient = handler.CreateClient();
        var client = new NebulaAchievementClient(httpClient, "achv_token", NullLoggerOf<NebulaAchievementClient>());

        await client.SyncCatalogAsync(new List<AchievementDef> { BuildDef() });

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SyncCatalogAsync_DoesNotThrow_WhenTheHttpCallThrows()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var httpClient = handler.CreateClient();
        var client = new NebulaAchievementClient(httpClient, "achv_token", NullLoggerOf<NebulaAchievementClient>());

        var exception = await Record.ExceptionAsync(() =>
            client.SyncCatalogAsync(new List<AchievementDef> { BuildDef() }));

        Assert.Null(exception);
    }

    // --- Progress push -------------------------------------------------------------------

    [Fact]
    public async Task ReportProgressAsync_PostsToProgressEndpoint_WithComputedLevelAndProgress()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");
        var httpClient = handler.CreateClient();
        var client = new NebulaAchievementClient(httpClient, "achv_secret", NullLoggerOf<NebulaAchievementClient>());
        var def = BuildDef("COL-01");
        var userId = Guid.NewGuid();

        await client.ReportProgressAsync(userId, def, value: 50, level: 3);

        Assert.Single(handler.Requests);
        var request = handler.Requests[0];
        Assert.Equal("/api/v1/achievements/progress", request.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("achv_secret", request.Headers.Authorization!.Parameter);

        using var body = JsonDocument.Parse(handler.RequestBodies[0]!);
        var entries = body.RootElement.GetProperty("entries");
        Assert.Equal(1, entries.GetArrayLength());

        var entry = entries[0];
        Assert.Equal(userId, entry.GetProperty("userId").GetGuid());
        Assert.Equal("COL-01", entry.GetProperty("externalId").GetString());
        Assert.Equal(50, entry.GetProperty("value").GetInt64());
        Assert.Equal(3, entry.GetProperty("level").GetInt32());
        // value=50 against a last threshold of 100 -> 50% -> 5000 basis points.
        Assert.Equal(5000, entry.GetProperty("progress").GetInt32());
    }

    [Fact]
    public async Task ReportProgressAsync_CapsProgressAtTenThousand_WhenValueExceedsMaxThreshold()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");
        var httpClient = handler.CreateClient();
        var client = new NebulaAchievementClient(httpClient, "achv_secret", NullLoggerOf<NebulaAchievementClient>());
        var def = BuildDef("COL-01");

        await client.ReportProgressAsync(Guid.NewGuid(), def, value: 1_000_000, level: 5);

        using var body = JsonDocument.Parse(handler.RequestBodies[0]!);
        var entry = body.RootElement.GetProperty("entries")[0];
        Assert.Equal(10000, entry.GetProperty("progress").GetInt32());
    }

    [Fact]
    public async Task ReportProgressAsync_DoesNotThrow_WhenTheHttpCallThrows()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var httpClient = handler.CreateClient();
        var client = new NebulaAchievementClient(httpClient, "achv_token", NullLoggerOf<NebulaAchievementClient>());

        var exception = await Record.ExceptionAsync(() =>
            client.ReportProgressAsync(Guid.NewGuid(), BuildDef(), 5, 1));

        Assert.Null(exception);
    }

    private static ILogger<T> NullLoggerOf<T>() => Mock.Of<ILogger<T>>();
}
