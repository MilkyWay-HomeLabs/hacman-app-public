using hacman_app_front.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace hacman_app_front.Tests.Services;

/// <summary>
/// Boots the real composition root (Program.cs) via WebApplicationFactory and verifies
/// DI lifetimes of per-user state holders. In Blazor Server one scope = one circuit = one
/// user session, so anything holding per-user data must never resolve to a shared instance
/// across scopes — that would leak one user's data to everyone else.
/// </summary>
public class ServiceRegistrationTest(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void UserGalleriesState_IsScopedPerCircuit_NotShared()
    {
        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();

        var stateA = scopeA.ServiceProvider.GetRequiredService<UserGalleriesState>();
        var stateB = scopeB.ServiceProvider.GetRequiredService<UserGalleriesState>();

        Assert.NotSame(stateA, stateB);
    }

    [Fact]
    public void UserGalleriesState_IsSameInstanceWithinOneScope()
    {
        using var scope = factory.Services.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<UserGalleriesState>();
        var second = scope.ServiceProvider.GetRequiredService<UserGalleriesState>();

        Assert.Same(first, second);
    }

    [Fact]
    public void UserSessionState_IsScopedPerCircuit_NotShared()
    {
        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();

        var stateA = scopeA.ServiceProvider.GetRequiredService<UserSessionState>();
        var stateB = scopeB.ServiceProvider.GetRequiredService<UserSessionState>();

        Assert.NotSame(stateA, stateB);
    }

    [Fact]
    public void AllHacmanApiServices_ResolveFromAScope()
    {
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserApiService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IScoresApiService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserGalleryApiService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<HacmanApiClientFactory>());
    }
}