using System.Net;
using Bunit;
using hacman_app_front.Components.Pages;
using hacman_app_front.Domain;
using hacman_app_front.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using DomainGallery = hacman_app_front.Domain.Gallery;

namespace hacman_app_front.Tests.Components;

/// <summary>
/// Covers Home's LoadUserName fallback chain (session -> hacmanUser localStorage -> userData
/// localStorage -> UserApiService -> stays ANONYMOUS) and RefreshData's progress/count math --
/// not exercised by HomeTest, which only guards the zone-card navigation regression.
/// </summary>
public class HomeAdditionalTest : BunitContext
{
    private readonly UserGalleriesState _galleries = new();
    private readonly Mock<ILocalStorageService> _localStorage = new();
    private readonly Mock<IUserApiService> _userApi = new();

    public HomeAdditionalTest()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppBasePath"] = "/hacman/app",
            ["HacmanDictionary:BookGroupName"] = "Book",
            ["HacmanDictionary:CharacterGroupName"] = "Character",
            ["HacmanDictionary:CosplayGroupName"] = "Cosplay"
        }).Build();

        Services.AddSingleton<IConfiguration>(config);
        Services.AddSingleton(_galleries);
        Services.AddSingleton(new UserSessionState());
        Services.AddSingleton(_localStorage.Object);
        Services.AddSingleton(_userApi.Object);

        _galleries.Set(Array.Empty<UserGallery>());
        _localStorage.Setup(l => l.GetItemAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
    }

    private static UserGallery Item(string typeName, int galleryId, bool isUnlocked) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        IsUnlocked = isUnlocked,
        Gallery = new DomainGallery
        {
            Id = Guid.NewGuid(),
            GalleryId = galleryId,
            Title = $"Gallery {galleryId}",
            Type = new LookupValue { Name = typeName },
            Subtype = new LookupValue { Name = "Sub" },
            Theme = new LookupValue { Name = "Theme" }
        }
    };

    [Fact]
    public void AnonymousState_ShowsAccessRestricted_AndLoginButtonRedirects()
    {
        var cut = Render<Home>();
        cut.WaitForAssertion(() => Assert.Contains("ACCESS_RESTRICTED", cut.Markup));

        cut.Find("button.matrix-btn.primary").Click();

        var navigation = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("nebula/app/redirect", navigation.Uri);
    }

    [Fact]
    public void LoadUserName_UsesHacmanUserFromLocalStorage_WhenSessionEmpty()
    {
        _localStorage.Setup(l => l.GetItemAsync("hacmanUser"))
            .ReturnsAsync("""{"id":"11111111-1111-1111-1111-111111111111","username":"morpheus"}""");

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("WELCOME_AGENT_MORPHEUS", cut.Markup));
    }

    [Fact]
    public void LoadUserName_FallsBackToUserDataInLocalStorage_WhenHacmanUserMissing()
    {
        _localStorage.Setup(l => l.GetItemAsync("userData")).ReturnsAsync("""{"login":"trinity"}""");

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("WELCOME_AGENT_TRINITY", cut.Markup));
    }

    [Fact]
    public void LoadUserName_IgnoresMalformedHacmanUserJson_AndFallsThroughToUserData()
    {
        _localStorage.Setup(l => l.GetItemAsync("hacmanUser")).ReturnsAsync("not-json");
        _localStorage.Setup(l => l.GetItemAsync("userData")).ReturnsAsync("""{"login":"switch"}""");

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("WELCOME_AGENT_SWITCH", cut.Markup));
    }

    [Fact]
    public void LoadUserName_FallsBackToUserApiService_WhenLocalStorageEmpty()
    {
        _userApi.Setup(a => a.GetUserByExternalAuthId())
            .ReturnsAsync(new UserResponse { Username = "tank" });

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("WELCOME_AGENT_TANK", cut.Markup));
    }

    [Fact]
    public void LoadUserName_RemainsAnonymous_WhenApiThrowsUnauthorized()
    {
        _userApi.Setup(a => a.GetUserByExternalAuthId())
            .ThrowsAsync(new HttpRequestException("no auth", null, HttpStatusCode.Unauthorized));

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("ACCESS_RESTRICTED", cut.Markup));
    }

    [Fact]
    public void LoadUserName_RemainsAnonymous_WhenApiThrowsGenericError()
    {
        _userApi.Setup(a => a.GetUserByExternalAuthId())
            .ThrowsAsync(new InvalidOperationException("network blip"));

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("ACCESS_RESTRICTED", cut.Markup));
    }

    [Fact]
    public void RefreshData_ComputesPerZoneProgressAndCounts()
    {
        Services.AddSingleton(new UserSessionState { CurrentUser = new UserResponse { Username = "neo" } });
        _galleries.Set([
            Item("Book", 1, isUnlocked: true),
            Item("Book", 2, isUnlocked: false),
            Item("Character", 3, isUnlocked: true)
        ]);

        var cut = Render<Home>();

        Assert.Contains("50% COMPLETE", cut.Markup);
        Assert.Contains("1 / 2 DECRYPTED", cut.Markup);
        Assert.Contains("1 / 1 DECRYPTED", cut.Markup);
    }

    [Fact]
    public void RefreshData_RendersUndiscoveredLevels_CappedAtFive()
    {
        Services.AddSingleton(new UserSessionState { CurrentUser = new UserResponse { Username = "neo" } });
        _galleries.Set(Enumerable.Range(1, 7).Select(i => Item("Book", i, isUnlocked: false)));

        var cut = Render<Home>();

        Assert.Contains("PRIORITY_TARGETS", cut.Markup);
        Assert.Equal(5, cut.FindAll(".matrix-undiscovered-section .matrix-card").Count);
    }
}
