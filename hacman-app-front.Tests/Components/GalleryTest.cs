using Bunit;
using hacman_app_front.Domain;
using hacman_app_front.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using GalleryPage = hacman_app_front.Components.Pages.Gallery;

namespace hacman_app_front.Tests.Components;

public class GalleryTest : BunitContext
{
    private readonly UserGalleriesState _state = new();
    private readonly Mock<IScoresApiService> _scoresApi = new();

    public GalleryTest()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppBasePath"] = "/hacman/app",
            ["HacmanResourcesUrl"] = "https://resources.test.local",
            ["HacmanDictionary:BookGroupName"] = "Book",
            ["HacmanDictionary:CharacterGroupName"] = "Character",
            ["HacmanDictionary:CosplayGroupName"] = "Cosplay"
        }).Build();

        Services.AddSingleton<IConfiguration>(config);
        Services.AddSingleton(_state);
        Services.AddSingleton(_scoresApi.Object);
        Services.AddSingleton(new UserSessionState { CurrentUser = new UserResponse { Username = "agent" } });
    }

    private static UserGallery Item(Guid id, string typeName = "Book", string subtypeName = "Novel",
        string themeName = "Matrix", int galleryId = 1, long? bestTimeMs = null, int imageCount = 10) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        IsUnlocked = true,
        BestTimeMs = bestTimeMs,
        Gallery = new Gallery
        {
            Id = id,
            GalleryId = galleryId,
            Title = "The Gallery",
            ImageCount = imageCount,
            Type = new LookupValue { Name = typeName },
            Subtype = new LookupValue { Name = subtypeName },
            Theme = new LookupValue { Name = themeName }
        }
    };

    private static UserGallery ItemWithoutLookups(Guid id) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        IsUnlocked = true,
        Gallery = new Gallery { Id = id, GalleryId = 1, Title = "Bare Gallery", ImageCount = 1 }
    };

    private IRenderedComponent<GalleryPage> RenderPage(Guid id) =>
        Render<GalleryPage>(ps => ps.Add(p => p.Id, id));

    [Fact]
    public void ShowsInitializingMessage_WhenGalleryNotFoundInState()
    {
        var id = Guid.NewGuid();
        _state.Set(Array.Empty<UserGallery>());

        var cut = RenderPage(id);

        Assert.Contains("INITIALIZING_ARCHIVE_DATA", cut.Markup);
    }

    [Fact]
    public void RendersGalleryDetails_WhenFound()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id, bestTimeMs: 65000)]);

        var cut = RenderPage(id);

        Assert.Contains("The Gallery", cut.Markup);
        Assert.Contains("Book", cut.Markup);
        Assert.Contains("Novel", cut.Markup);
        Assert.Contains("Matrix", cut.Markup);
        Assert.Contains("01:05", cut.Markup);
    }

    [Fact]
    public void ShowsNotAvailable_WhenBestTimeIsNull()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id, bestTimeMs: null)]);

        var cut = RenderPage(id);

        Assert.Contains("N/A", cut.Markup);
    }

    [Fact]
    public void RendersDefaultImageCount_WhenImageCountIsTen()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id, imageCount: 3)]);

        var cut = RenderPage(id);

        var thumbs = cut.FindAll("img.matrix-gallery-thumb");
        Assert.Equal(3, thumbs.Count);
        Assert.Contains("https://resources.test.local/img/galleries/1/1.jpg", thumbs[0].GetAttribute("src"));
    }

    [Fact]
    public void BackToArchiveButton_NavigatesToResolvedTypeRoute()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id, typeName: "Character")]);
        var cut = RenderPage(id);
        var navigation = Services.GetRequiredService<NavigationManager>();

        cut.Find("button.matrix-filter-btn").Click();

        Assert.EndsWith("/hacman/app/character", navigation.Uri);
    }

    [Fact]
    public void ClickingTypeButton_NavigatesToResolvedTypeRoute()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id, typeName: "Book")]);
        var cut = RenderPage(id);
        var navigation = Services.GetRequiredService<NavigationManager>();

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Book").Click();

        Assert.EndsWith("/hacman/app/book", navigation.Uri);
    }

    [Fact]
    public void ClickingSubtypeButton_NavigatesWithFilterQueryParam()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id, typeName: "Book", subtypeName: "Novel")]);
        var cut = RenderPage(id);
        var navigation = Services.GetRequiredService<NavigationManager>();

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Novel").Click();

        Assert.Contains("/hacman/app/book?filter=Novel", navigation.Uri);
    }

    [Fact]
    public void ClickingThemeButton_NavigatesToThemePageWithFilter()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id, themeName: "Matrix")]);
        var cut = RenderPage(id);
        var navigation = Services.GetRequiredService<NavigationManager>();

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Matrix").Click();

        Assert.Contains("/hacman/app/theme?filter=Matrix", navigation.Uri);
    }

    [Fact]
    public async Task ToggleScores_LoadsAndDisplaysScoresGroupedByDifficulty()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id)]);
        _scoresApi.Setup(s => s.GetScores(id)).ReturnsAsync([
            new ScoreEntry { Username = "neo", TimeMs = 42_500, Difficulty = "HARD", AchievedAt = DateTime.UtcNow }
        ]);
        var cut = RenderPage(id);

        await cut.InvokeAsync(() => cut.Find("button.matrix-btn.success").Click());
        cut.WaitForAssertion(() => Assert.Contains("neo", cut.Markup));

        Assert.Contains("HIDE_RANKINGS", cut.Markup);
        Assert.Contains("00:42.500", cut.Markup);
    }

    [Fact]
    public async Task ToggleScores_ShowsEmptyState_WhenNoScoresReturned()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id)]);
        _scoresApi.Setup(s => s.GetScores(id)).ReturnsAsync([]);
        var cut = RenderPage(id);

        await cut.InvokeAsync(() => cut.Find("button.matrix-btn.success").Click());

        cut.WaitForAssertion(() => Assert.Contains("NO DATA RECORDED IN THE ARCHIVES", cut.Markup));
    }

    [Fact]
    public async Task ToggleScores_TwiceHidesRankingsAgain_WithoutReloading()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id)]);
        _scoresApi.Setup(s => s.GetScores(id)).ReturnsAsync([]);
        var cut = RenderPage(id);

        await cut.InvokeAsync(() => cut.Find("button.matrix-btn.success").Click());
        cut.WaitForAssertion(() => Assert.Contains("HIDE_RANKINGS", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("button.matrix-btn.success").Click());

        Assert.Contains("SCORE_RANKINGS", cut.Markup);
        _scoresApi.Verify(s => s.GetScores(id), Times.Once);
    }

    [Fact]
    public void OpeningLightbox_ShowsOverlayWithCurrentImage()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id, imageCount: 5)]);
        var cut = RenderPage(id);

        cut.FindAll("img.matrix-gallery-thumb")[2].Click();

        Assert.Contains("matrix-lightbox-overlay", cut.Markup);
        Assert.Contains("3 / 5", cut.Markup);
    }

    [Fact]
    public void Lightbox_NextAndPrev_WrapAround()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id, imageCount: 3)]);
        var cut = RenderPage(id);
        cut.FindAll("img.matrix-gallery-thumb")[0].Click();
        Assert.Contains("1 / 3", cut.Markup);

        cut.FindAll("button.matrix-lightbox-btn").First(b => b.TextContent.Contains("◀")).Click();
        Assert.Contains("3 / 3", cut.Markup);

        cut.FindAll("button.matrix-lightbox-btn").First(b => b.TextContent.Contains("▶")).Click();
        Assert.Contains("1 / 3", cut.Markup);

        cut.FindAll("button.matrix-lightbox-btn").First(b => b.TextContent.Contains("▶")).Click();
        Assert.Contains("2 / 3", cut.Markup);
    }

    [Fact]
    public void ClosingLightbox_RemovesOverlay()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id)]);
        var cut = RenderPage(id);
        cut.FindAll("img.matrix-gallery-thumb")[0].Click();

        cut.Find("button.matrix-lightbox-close").Click();

        Assert.DoesNotContain("matrix-lightbox-overlay", cut.Markup);
    }

    [Fact]
    public void ChangingIdParameter_ResetsScoresAndRefreshesGallery()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _state.Set([Item(id1, galleryId: 1), Item(id2, galleryId: 2)]);
        var cut = RenderPage(id1);
        Assert.Contains("The Gallery", cut.Markup);

        cut.Render(ps => ps.Add(p => p.Id, id2));

        Assert.Contains("The Gallery", cut.Markup);
    }

    [Fact]
    public void TypeSubtypeThemeButtons_AreHidden_WhenLookupsAreNull()
    {
        var id = Guid.NewGuid();
        _state.Set([ItemWithoutLookups(id)]);

        var cut = RenderPage(id);

        // Only the fixed "BACK_TO_ARCHIVE" and "SCORE_RANKINGS" buttons remain -- no
        // Type/Subtype/Theme buttons render since none of those lookups are set.
        var buttons = cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Equal(2, buttons.Count);
        Assert.Contains(buttons, b => b.Contains("BACK_TO_ARCHIVE"));
        Assert.Contains(buttons, b => b.Contains("SCORE_RANKINGS"));
    }

    [Fact]
    public async Task ScoresTable_MarksCurrentUsersRow()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id)]);
        _scoresApi.Setup(s => s.GetScores(id)).ReturnsAsync([
            new ScoreEntry { Username = "agent", TimeMs = 1_000, Difficulty = "EASY", AchievedAt = DateTime.UtcNow }
        ]);
        var cut = RenderPage(id);

        await cut.InvokeAsync(() => cut.Find("button.matrix-btn.success").Click());

        cut.WaitForAssertion(() => Assert.Contains("matrix-row-current-user", cut.Markup));
    }

    [Fact]
    public async Task ToggleScores_PropagatesException_WhenApiCallFails()
    {
        var id = Guid.NewGuid();
        _state.Set([Item(id)]);
        _scoresApi.Setup(s => s.GetScores(id)).ThrowsAsync(new InvalidOperationException("boom"));
        var cut = RenderPage(id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cut.InvokeAsync(() => cut.Find("button.matrix-btn.success").Click()));
    }
}
