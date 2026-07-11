using Bunit;
using hacman_app_front.Components.Shared;
using hacman_app_front.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace hacman_app_front.Tests.Components;

public class GalleryCardTest : BunitContext
{
    private const string ResourcesUrl = "https://resources.test.local";
    private const string GameUrl = "https://game.test.local";
    private const string BasePath = "/hacman/app";

    public GalleryCardTest()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppBasePath"] = BasePath,
            ["HacmanResourcesUrl"] = ResourcesUrl,
            ["HacmanGameUrl"] = GameUrl
        }).Build();
        Services.AddSingleton<IConfiguration>(config);
    }

    private static UserGallery CreateItem(
        bool isUnlocked = true, long? bestTimeMs = null, int galleryNumber = 7, string title = "The Construct")
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            IsUnlocked = isUnlocked,
            BestTimeMs = bestTimeMs,
            Gallery = new Gallery
            {
                Id = Guid.Parse("12345678-aaaa-bbbb-cccc-000000000000"),
                GalleryId = galleryNumber,
                Title = title,
                Subtype = new LookupValue { Name = "Novel" }
            }
        };

    private IRenderedComponent<GalleryCard> RenderCard(UserGallery item, bool showSubtype = false)
        => Render<GalleryCard>(ps => ps
            .Add(p => p.Item, item)
            .Add(p => p.ShowSubtype, showSubtype));

    [Fact]
    public void RendersTitleAndShortId_WhenUnlocked()
    {
        var cut = RenderCard(CreateItem(isUnlocked: true, title: "The Construct"));

        Assert.Contains("The Construct", cut.Markup);
        Assert.Contains("#12345678", cut.Markup);
        Assert.DoesNotContain("ACCESS_DENIED", cut.Markup);
        Assert.DoesNotContain("LOCKED", cut.Markup);
    }

    [Fact]
    public void RendersAccessDeniedAndLockedOverlay_WhenLocked()
    {
        var cut = RenderCard(CreateItem(isUnlocked: false, title: "The Construct"));

        Assert.Contains("ACCESS_DENIED", cut.Markup);
        Assert.Contains("LOCKED", cut.Markup);
        Assert.Contains("UNAVAILABLE", cut.Markup);
        Assert.DoesNotContain("The Construct", cut.Markup);
    }

    [Fact]
    public void UsesUnlockedImageVariant_WithConfiguredResourcesUrl()
    {
        var cut = RenderCard(CreateItem(isUnlocked: true, galleryNumber: 7));

        var img = cut.Find("img");
        Assert.Equal($"{ResourcesUrl}/img/galleries/7/0_100.jpg", img.GetAttribute("src"));
        Assert.Contains($"{ResourcesUrl}/img/galleries/missing.jpg", img.GetAttribute("onerror"));
    }

    [Fact]
    public void UsesLockedImageVariant_WhenLocked()
    {
        var cut = RenderCard(CreateItem(isUnlocked: false, galleryNumber: 7));

        var img = cut.Find("img");
        Assert.Equal($"{ResourcesUrl}/img/galleries/7/0_0.jpg", img.GetAttribute("src"));
    }

    [Fact]
    public void FormatsBestTime_AsMinutesAndSeconds()
    {
        var cut = RenderCard(CreateItem(bestTimeMs: 83_000)); // 1 min 23 s

        Assert.Contains("BEST: 01:23", cut.Markup);
    }

    [Fact]
    public void ShowsNA_WhenNoBestTimeRecorded()
    {
        var cut = RenderCard(CreateItem(bestTimeMs: null));

        Assert.Contains("BEST: N/A", cut.Markup);
    }

    [Fact]
    public void ShowsSubtype_OnlyWhenRequested()
    {
        var withSubtype = RenderCard(CreateItem(), showSubtype: true);
        var withoutSubtype = RenderCard(CreateItem(), showSubtype: false);

        Assert.Contains("SUB: Novel", withSubtype.Markup);
        Assert.DoesNotContain("SUB:", withoutSubtype.Markup);
    }

    [Fact]
    public void DisablesViewAndRankingsButtons_WhenLocked()
    {
        var cut = RenderCard(CreateItem(isUnlocked: false));

        var buttons = cut.FindAll("button");
        var view = buttons.Single(b => b.TextContent.Contains("VIEW"));
        var rankings = buttons.Single(b => b.TextContent.Contains("RANKINGS"));
        var play = buttons.Single(b => b.TextContent.Contains("PLAY"));

        Assert.True(view.HasAttribute("disabled"));
        Assert.True(rankings.HasAttribute("disabled"));
        Assert.False(play.HasAttribute("disabled")); // PLAY stays enabled even for locked galleries
    }

    [Fact]
    public void ViewButton_NavigatesToGalleryPage()
    {
        var item = CreateItem(isUnlocked: true);
        var cut = RenderCard(item);
        var navigation = Services.GetRequiredService<NavigationManager>();

        cut.FindAll("button").Single(b => b.TextContent.Contains("VIEW")).Click();

        Assert.EndsWith($"{BasePath}/gallery/{item.Gallery.Id}", navigation.Uri);
    }

    [Fact]
    public void PlayButton_NavigatesToGameUrl_WithGalleryIdAndTitle()
    {
        var item = CreateItem(title: "The Construct");
        var cut = RenderCard(item);
        var navigation = Services.GetRequiredService<NavigationManager>();

        cut.FindAll("button").Single(b => b.TextContent.Contains("PLAY")).Click();

        Assert.StartsWith($"{GameUrl}/play", navigation.Uri);
        Assert.Contains($"galleryId={item.Gallery.Id}", navigation.Uri);
        // Title is URL-encoded exactly once by QueryHelpers.AddQueryString ("The Construct" → "The%20Construct").
        // FR-8 removed the earlier Uri.EscapeDataString that double-encoded it to "The%2520Construct".
        Assert.Contains("title=The%20Construct", navigation.Uri);
        Assert.DoesNotContain("The%2520Construct", navigation.Uri);
    }
}