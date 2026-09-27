using System.Net;
using System.Text.Json;
using hacman_app_front.Services;
using hacman_app_front.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace hacman_app_front.Tests.Services;

public class UserApiServiceTest
{
    private static UserApiService CreateService(FakeHttpMessageHandler handler)
        => new(handler.CreateClient(), NullLogger<UserApiService>.Instance);

    private static string UserJson(Guid id, string username, string externalAuthId = "1000011")
        => $$"""
             {
               "id": "{{id}}",
               "externalAuthId": "{{externalAuthId}}",
               "username": "{{username}}",
               "email": "{{username}}@example.com",
               "createdAt": "2026-01-01T00:00:00Z",
               "updatedAt": "2026-01-01T00:00:00Z"
             }
             """;

    // --- GetUserByExternalAuthId ---

    [Fact]
    public async Task GetUserByExternalAuthId_ReturnsUser_WhenResponseIsOk()
    {
        var userId = Guid.NewGuid();
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, UserJson(userId, "agent"));
        var service = CreateService(handler);

        var user = await service.GetUserByExternalAuthId();

        Assert.NotNull(user);
        Assert.Equal(userId, user.Id);
        Assert.Equal("agent", user.Username);
        Assert.Equal("1000011", user.ExternalAuthId);
    }

    [Fact]
    public async Task GetUserByExternalAuthId_SendsGetToExpectedUrl()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, UserJson(Guid.NewGuid(), "agent"));
        var service = CreateService(handler);

        await service.GetUserByExternalAuthId();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.test.local/v1/users/external-auth-id", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetUserByExternalAuthId_ReturnsNull_WhenUserDoesNotExist()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.NotFound);
        var service = CreateService(handler);

        var user = await service.GetUserByExternalAuthId();

        Assert.Null(user);
    }

    [Fact]
    public async Task GetUserByExternalAuthId_ThrowsUnauthorized_WhenResponseIs401()
    {
        var handler = FakeHttpMessageHandler.Returning(
            HttpStatusCode.Unauthorized, """{"message":"Invalid token"}""");
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            service.GetUserByExternalAuthId);

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Contains("Invalid token", ex.Message);
    }

    [Fact]
    public async Task GetUserByExternalAuthId_Throws_WhenResponseIsServerError()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.InternalServerError);
        var service = CreateService(handler);

        await Assert.ThrowsAsync<HttpRequestException>(service.GetUserByExternalAuthId);
    }

    // --- CreateNewUser ---

    [Fact]
    public async Task CreateNewUser_ReturnsCreatedUser_WhenResponseIsSuccessful()
    {
        var userId = Guid.NewGuid();
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.Created, UserJson(userId, "neo"));
        var service = CreateService(handler);

        var user = await service.CreateNewUser("neo");

        Assert.NotNull(user);
        Assert.Equal(userId, user.Id);
        Assert.Equal("neo", user.Username);
    }

    [Fact]
    public async Task CreateNewUser_SendsPostWithUsernameInBody()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.Created, UserJson(Guid.NewGuid(), "neo"));
        var service = CreateService(handler);

        await service.CreateNewUser("neo");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.test.local/v1/users", request.RequestUri!.ToString());

        // PostAsJsonAsync serializes with JsonSerializerDefaults.Web, so the property is camelCased.
        using var body = JsonDocument.Parse(handler.RequestBodies[0]!);
        Assert.Equal("neo", body.RootElement.GetProperty("username").GetString());
    }

    [Fact]
    public async Task CreateNewUser_ThrowsUnauthorized_WhenResponseIs401()
    {
        var handler = FakeHttpMessageHandler.Returning(
            HttpStatusCode.Unauthorized, """{"message":"User is not mapped to this identity"}""");
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => service.CreateNewUser("neo"));

        Assert.Contains("User is not mapped to this identity", ex.Message);
    }

    [Fact]
    public async Task CreateNewUser_Throws_WhenResponseIsBadRequest()
    {
        var handler = FakeHttpMessageHandler.Returning(
            HttpStatusCode.BadRequest, """{"message":"Username is required"}""");
        var service = CreateService(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.CreateNewUser(""));
    }
}