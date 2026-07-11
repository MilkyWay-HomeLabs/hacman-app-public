using System.Net;
using System.Text.Json;
using hacman_app_front.Services;
using hacman_app_front.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace hacman_app_front.Tests.Services;

public class UserGalleryApiServiceTest
{
    private static UserGalleryApiService CreateService(FakeHttpMessageHandler handler)
        => new(handler.CreateClient(), NullLogger<UserGalleryApiService>.Instance);

    private static string UserGalleryJson(Guid galleryId, string title, bool isUnlocked, long? bestTimeMs = null)
        => $$"""
             {
               "id": "{{Guid.NewGuid()}}",
               "userId": "{{Guid.NewGuid()}}",
               "isUnlocked": {{isUnlocked.ToString().ToLowerInvariant()}},
               "bestTimeMs": {{(bestTimeMs?.ToString() ?? "null")}},
               "createdAt": "2026-01-01T00:00:00Z",
               "updatedAt": "2026-01-01T00:00:00Z",
               "gallery": {
                 "id": "{{galleryId}}",
                 "title": "{{title}}",
                 "galleryId": 1,
                 "imageCount": 10,
                 "level": 1
               }
             }
             """;

    [Fact]
    public async Task GetUserGalleries_ReturnsDeserializedGalleries_WhenResponseIsOk()
    {
        var galleryId = Guid.NewGuid();
        var json = $"[{UserGalleryJson(galleryId, "The Construct", isUnlocked: true, bestTimeMs: 42000)}]";
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var galleries = await service.GetUserGalleries(Guid.NewGuid());

        var gallery = Assert.Single(galleries);
        Assert.True(gallery.IsUnlocked);
        Assert.Equal(42000L, gallery.BestTimeMs);
        Assert.Equal(galleryId, gallery.Gallery.Id);
        Assert.Equal("The Construct", gallery.Gallery.Title);
    }

    [Fact]
    public async Task GetUserGalleries_SendsGetWithUserIdInQuery()
    {
        var userId = Guid.NewGuid();
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "[]");
        var service = CreateService(handler);

        await service.GetUserGalleries(userId);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"https://api.test.local/v1/user-galleries?userId={userId}", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetUserGalleries_ReturnsEmptyList_WhenBodyIsJsonNull()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "null");
        var service = CreateService(handler);

        var galleries = await service.GetUserGalleries(Guid.NewGuid());

        Assert.NotNull(galleries);
        Assert.Empty(galleries);
    }

    [Fact]
    public async Task GetUserGalleries_Throws_WhenResponseIsError()
    {
        var handler = FakeHttpMessageHandler.Returning(
            HttpStatusCode.Unauthorized, """{"message":"Invalid token claims"}""");
        var service = CreateService(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetUserGalleries(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetUserGalleries_ThrowsJsonException_WhenBodyIsMalformed()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "<html>not json</html>");
        var service = CreateService(handler);

        await Assert.ThrowsAsync<JsonException>(() => service.GetUserGalleries(Guid.NewGuid()));
    }
}