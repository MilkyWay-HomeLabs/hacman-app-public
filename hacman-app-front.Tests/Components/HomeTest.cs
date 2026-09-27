using Bunit;
using hacman_app_front.Components.Pages;
using hacman_app_front.Domain;
using hacman_app_front.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace hacman_app_front.Tests.Components;

/// <summary>
/// Guards the home-page zone cards' navigation. They must target the dedicated gallery-type
/// routes ("/book"/"/character"/"/cosplay"), not the raw configured group name ("/Type 1"),
/// which 404s since the Archive catch-all page was retired.
/// </summary>
public class HomeTest : BunitContext
{
    private readonly UserGalleriesState _galleries = new();

    public HomeTest()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppBasePath"] = "/hacman/app",
            // The real deployment maps the three gallery types to "Type 1/2/3" group names.
            ["HacmanDictionary:BookGroupName"] = "Type 1",
            ["HacmanDictionary:CharacterGroupName"] = "Type 2",
            ["HacmanDictionary:CosplayGroupName"] = "Type 3"
        }).Build();

        Services.AddSingleton<IConfiguration>(config);
        Services.AddSingleton(_galleries);
        // A logged-in session so Home renders the zone cards (OnInitialized reads the username).
        Services.AddSingleton(new UserSessionState { CurrentUser = new UserResponse { Username = "agent" } });
        Services.AddSingleton(Mock.Of<ILocalStorageService>());
        Services.AddSingleton(Mock.Of<IUserApiService>());

        _galleries.Set(Array.Empty<UserGallery>());
    }

    [Theory]
    [InlineData(0, "/hacman/app/book")]
    [InlineData(1, "/hacman/app/character")]
    [InlineData(2, "/hacman/app/cosplay")]
    public void ZoneCard_NavigatesToDedicatedRoute_NotRetiredArchivePath(int cardIndex, string expectedPath)
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = Render<Home>();

        var cards = cut.FindAll(".matrix-zone-card");
        Assert.Equal(3, cards.Count);

        cards[cardIndex].Click();

        Assert.Equal(nav.BaseUri.TrimEnd('/') + expectedPath, nav.Uri);
        // Regression guards: the old code produced "/Type%201" etc.
        Assert.DoesNotContain("Type", nav.Uri);
        Assert.DoesNotContain("%20", nav.Uri);
    }
}