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

public class ScoresTest : BunitContext
{
    private readonly UserGalleriesState _state = new();
    private readonly Mock<IScoresApiService> _scoresApi = new();

    public ScoresTest()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppBasePath"] = "/hacman/app",
            ["HacmanResourcesUrl"] = "https://resources.test.local",
            ["HacmanGameUrl"] = "https://game.test.local"
        }).Build();

        Services.AddSingleton<IConfiguration>(config);
        Services.AddSingleton(_state);
        Services.AddSingleton(_scoresApi.Object);
        Services.AddSingleton(new UserSessionState { CurrentUser = new UserResponse { Username = "neo" } });
    }

    private static UserGallery Item(string typeName, int galleryId, bool isUnlocked = true, long? bestTimeMs = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        IsUnlocked = isUnlocked,
        BestTimeMs = bestTimeMs,
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

    private IRenderedComponent<Scores> RenderPage(Guid? selectedId = null) =>
        Render<Scores>(ps => ps.Add(p => p.SelectedGalleryId, selectedId));

    [Fact]
    public void RendersGalleriesGroupedByType_WithCounts()
    {
        _state.Set([Item("Book", 1), Item("Book", 2), Item("Character", 3)]);

        var cut = RenderPage();

        Assert.Contains("TYPE: Book", cut.Markup);
        Assert.Contains("TYPE: Character", cut.Markup);
        var buttons = cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains(buttons, b => b.Contains("All") && b.Contains("3"));
        Assert.Contains(buttons, b => b.Contains("Book") && b.Contains("2"));
    }

    [Fact]
    public void LockedGallery_ShowsAccessDenied()
    {
        _state.Set([Item("Book", 1, isUnlocked: false)]);

        var cut = RenderPage();

        Assert.Contains("ACCESS_DENIED", cut.Markup);
    }

    [Fact]
    public void SelectingType_FiltersSections()
    {
        _state.Set([Item("Book", 1), Item("Character", 2)]);
        var cut = RenderPage();

        cut.FindAll("button").Single(b => b.TextContent.Trim().StartsWith("Book")).Click();

        Assert.Contains("TYPE: Book", cut.Markup);
        Assert.DoesNotContain("TYPE: Character", cut.Markup);
    }

    [Fact]
    public async Task ExpandingScoresRow_LoadsAndShowsScores()
    {
        var gallery = Item("Book", 1);
        _state.Set([gallery]);
        _scoresApi.Setup(s => s.GetScores(gallery.Gallery.Id)).ReturnsAsync([
            new ScoreEntry { Username = "neo", TimeMs = 12_345, Difficulty = "EASY", AchievedAt = DateTime.UtcNow }
        ]);
        var cut = RenderPage();

        await cut.InvokeAsync(() => cut.Find("button.matrix-btn.success").Click());
        cut.WaitForAssertion(() => Assert.Contains("00:12.345", cut.Markup));

        Assert.Contains("matrix-row-current-user", cut.Markup);
    }

    [Fact]
    public async Task CollapsingExpandedRow_ClearsScores()
    {
        var gallery = Item("Book", 1);
        _state.Set([gallery]);
        _scoresApi.Setup(s => s.GetScores(gallery.Gallery.Id)).ReturnsAsync([]);
        var cut = RenderPage();
        await cut.InvokeAsync(() => cut.Find("button.matrix-btn.success").Click());
        cut.WaitForAssertion(() => Assert.Contains("HIDE", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("button.matrix-btn.success").Click());

        Assert.Contains("SCORES", cut.Markup);
        Assert.DoesNotContain("NO DATA RECORDED", cut.Markup);
    }

    [Fact]
    public void ClickingRow_TogglesScoresJustLikeButton()
    {
        var gallery = Item("Book", 1);
        _state.Set([gallery]);
        _scoresApi.Setup(s => s.GetScores(gallery.Gallery.Id)).ReturnsAsync([]);
        var cut = RenderPage();

        cut.Find("div.matrix-list-row").Click();

        cut.WaitForAssertion(() => Assert.Contains("HIDE", cut.Markup));
    }

    [Fact]
    public void ViewButton_NavigatesToGalleryDetailPage()
    {
        var gallery = Item("Book", 1);
        _state.Set([gallery]);
        var cut = RenderPage();
        var navigation = Services.GetRequiredService<NavigationManager>();

        cut.FindAll("button.matrix-btn.primary").Single().Click();

        Assert.Contains($"/hacman/app/gallery/{gallery.Gallery.Id}", navigation.Uri);
    }

    [Fact]
    public void ViewButton_IsDisabled_WhenGalleryLocked()
    {
        _state.Set([Item("Book", 1, isUnlocked: false)]);
        var cut = RenderPage();

        var viewButton = cut.FindAll("button.matrix-btn.primary").Single();
        Assert.True(viewButton.HasAttribute("disabled"));
    }

    [Fact]
    public void PlayButton_NavigatesToGameUrl_WithGalleryIdAndTitle()
    {
        var gallery = Item("Book", 1);
        _state.Set([gallery]);
        var cut = RenderPage();
        var navigation = Services.GetRequiredService<NavigationManager>();

        cut.FindAll("button.matrix-btn.info").Single().Click();

        Assert.StartsWith("https://game.test.local/play?galleryId=", navigation.Uri);
        Assert.Contains($"galleryId={gallery.Gallery.Id}", navigation.Uri);
        // Play double-encodes the title (Uri.EscapeDataString, then QueryHelpers.AddQueryString
        // escapes it again) -- pre-existing behavior, asserted as-is rather than "fixed" here.
        Assert.Contains("title=Gallery%25201", navigation.Uri);
    }

    [Fact]
    public void SelectedGalleryIdParameter_AutoExpandsAndLoadsScores()
    {
        var gallery = Item("Book", 1);
        _state.Set([gallery]);
        _scoresApi.Setup(s => s.GetScores(gallery.Gallery.Id)).ReturnsAsync([
            new ScoreEntry { Username = "trinity", TimeMs = 5_000, Difficulty = "MEDIUM", AchievedAt = DateTime.UtcNow }
        ]);

        var cut = RenderPage(selectedId: gallery.Gallery.Id);

        cut.WaitForAssertion(() => Assert.Contains("trinity", cut.Markup));
    }

    [Fact]
    public void UnlockedGallery_ShowsBestTime()
    {
        _state.Set([Item("Book", 1, bestTimeMs: 90_000)]);

        var cut = RenderPage();

        Assert.Contains("01:30", cut.Markup);
    }
}
