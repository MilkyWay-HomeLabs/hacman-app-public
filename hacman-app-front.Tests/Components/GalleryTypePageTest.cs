using Bunit;
using hacman_app_front.Components.Shared;
using hacman_app_front.Domain;
using hacman_app_front.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace hacman_app_front.Tests.Components;

public class GalleryTypePageTest : BunitContext
{
    private readonly UserGalleriesState _state = new();

    public GalleryTypePageTest()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppBasePath"] = "/hacman/app",
            ["HacmanDictionary:BookGroupName"] = "Book",
            ["HacmanResourcesUrl"] = "https://resources.test.local",
            ["HacmanGameUrl"] = "https://game.test.local"
        }).Build();
        Services.AddSingleton<IConfiguration>(config);
        Services.AddSingleton(_state);
    }

    private static UserGallery Item(string typeName, string subtypeName, int galleryNumber, bool isUnlocked = true)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            IsUnlocked = isUnlocked,
            Gallery = new Gallery
            {
                Id = Guid.NewGuid(),
                GalleryId = galleryNumber,
                Title = $"Gallery {galleryNumber}",
                Type = new LookupValue { Name = typeName },
                Subtype = new LookupValue { Name = subtypeName }
            }
        };

    private IRenderedComponent<GalleryTypePage> RenderPage(string? filter = null)
        => Render<GalleryTypePage>(ps => ps
            .Add(p => p.RouteSegment, "book")
            .Add(p => p.ConfigKey, "HacmanDictionary:BookGroupName")
            .Add(p => p.DefaultGroupName, "Book")
            .Add(p => p.Title, "Book Levels")
            .Add(p => p.Filter, filter));

    [Fact]
    public void RendersTitle()
    {
        _state.Set(Array.Empty<UserGallery>());

        var cut = RenderPage();

        Assert.Contains("Book Levels", cut.Find("h3").TextContent);
    }

    [Fact]
    public void FiltersGalleries_ByConfiguredGroupNameOnly()
    {
        _state.Set([
            Item("Book", "Novel", 1),
            Item("Book", "Novel", 2),
            Item("Character", "Hero", 3)
        ]);

        var cut = RenderPage();

        Assert.Contains("Total: 2", cut.Markup);
    }

    [Fact]
    public void FiltersGalleries_ByContainsMatch_ForNumberedTypes()
    {
        // "Book 1", "Book 2" etc. should still match the "Book" group name via Contains.
        _state.Set([Item("Book 1", "Novel", 1), Item("Book 2", "Novel", 2)]);

        var cut = RenderPage();

        Assert.Contains("Total: 2", cut.Markup);
    }

    [Fact]
    public void GroupsBySubtype_AndOrdersGroupsByLowestGalleryId()
    {
        _state.Set([
            Item("Book", "Zeta", 5),
            Item("Book", "Alpha", 1),
            Item("Book", "Alpha", 2)
        ]);

        var cut = RenderPage();

        var headings = cut.FindAll("h4").Select(h => h.TextContent).ToList();
        Assert.Equal(["Alpha", "Zeta"], headings);
    }

    [Fact]
    public void FilterButtons_ShowCountsPerSubtype()
    {
        _state.Set([
            Item("Book", "Novel", 1),
            Item("Book", "Novel", 2),
            Item("Book", "Comic", 3)
        ]);

        var cut = RenderPage();

        var buttons = cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains(buttons, b => b.Contains("All") && b.Contains("3"));
        Assert.Contains(buttons, b => b.Contains("Novel") && b.Contains("2"));
        Assert.Contains(buttons, b => b.Contains("Comic") && b.Contains("1"));
    }

    [Fact]
    public void AppliesInitialFilter_FromParameter()
    {
        _state.Set([Item("Book", "Novel", 1), Item("Book", "Comic", 2)]);

        var cut = RenderPage(filter: "Comic");

        var headings = cut.FindAll("h4").Select(h => h.TextContent).ToList();
        Assert.Equal(["Comic"], headings);
    }

    [Fact]
    public void ActiveFilterButton_HasActiveCssClass()
    {
        _state.Set([Item("Book", "Novel", 1)]);

        var cut = RenderPage(filter: "Novel");

        var novelButton = cut.FindAll("button").Single(b => b.TextContent.Contains("Novel"));
        Assert.Contains("active", novelButton.GetAttribute("class"));
    }

    [Fact]
    public void ClickingAllFilterButton_NavigatesToBaseRoute()
    {
        _state.Set([Item("Book", "Novel", 1)]);
        var cut = RenderPage(filter: "Novel");
        var navigation = Services.GetRequiredService<NavigationManager>();

        cut.FindAll("button").Single(b => b.TextContent.Contains("All")).Click();

        Assert.EndsWith("/hacman/app/book", navigation.Uri);
    }

    [Fact]
    public void ClickingSubtypeFilterButton_NavigatesWithFilterQueryParam()
    {
        _state.Set([Item("Book", "Novel", 1), Item("Book", "Comic", 2)]);
        var cut = RenderPage();
        var navigation = Services.GetRequiredService<NavigationManager>();

        cut.FindAll("button").Single(b => b.TextContent.Contains("Comic")).Click();

        Assert.Contains("/hacman/app/book?filter=Comic", navigation.Uri);
    }

    [Fact]
    public void RefreshesAutomatically_WhenUserGalleriesStateChanges()
    {
        _state.Set(Array.Empty<UserGallery>());
        var cut = RenderPage();
        Assert.Contains("Total: 0", cut.Markup);

        cut.InvokeAsync(() => _state.Set([Item("Book", "Novel", 1)]));

        cut.WaitForAssertion(() => Assert.Contains("Total: 1", cut.Markup));
    }
}