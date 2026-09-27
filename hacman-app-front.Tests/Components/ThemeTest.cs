using Bunit;
using hacman_app_front.Components.Pages;
using hacman_app_front.Domain;
using hacman_app_front.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DomainGallery = hacman_app_front.Domain.Gallery;

namespace hacman_app_front.Tests.Components;

public class ThemeTest : BunitContext
{
    private readonly UserGalleriesState _state = new();

    public ThemeTest()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppBasePath"] = "/hacman/app",
            ["HacmanResourcesUrl"] = "https://resources.test.local"
        }).Build();

        Services.AddSingleton<IConfiguration>(config);
        Services.AddSingleton(_state);
    }

    private static UserGallery Item(string themeName, string typeName, int galleryId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        IsUnlocked = true,
        Gallery = new DomainGallery
        {
            Id = Guid.NewGuid(),
            GalleryId = galleryId,
            Title = $"Gallery {galleryId}",
            Type = new LookupValue { Name = typeName },
            Subtype = new LookupValue { Name = "Sub" },
            Theme = new LookupValue { Name = themeName }
        }
    };

    private IRenderedComponent<Theme> RenderPage(string? filter = null)
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        if (filter != null)
        {
            navigation.NavigateTo(navigation.GetUriWithQueryParameter("Filter", filter));
        }

        return Render<Theme>();
    }

    [Fact]
    public void RendersThemeGroups_WithCounts()
    {
        _state.Set([Item("Matrix", "Book", 1), Item("Matrix", "Character", 2), Item("Zion", "Book", 3)]);

        var cut = RenderPage();

        var buttons = cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains(buttons, b => b.Contains("All") && b.Contains("3"));
        Assert.Contains(buttons, b => b.Contains("Matrix") && b.Contains("2"));
        Assert.Contains(buttons, b => b.Contains("Zion") && b.Contains("1"));
    }

    [Fact]
    public void ShowsAllThemesAndSubGroupedByType_ByDefault()
    {
        _state.Set([Item("Matrix", "Book", 1), Item("Zion", "Character", 2)]);

        var cut = RenderPage();

        Assert.Contains("THEME: Matrix", cut.Markup);
        Assert.Contains("THEME: Zion", cut.Markup);
        Assert.Contains("TYPE: Book", cut.Markup);
        Assert.Contains("TYPE: Character", cut.Markup);
    }

    [Fact]
    public void InitialFilter_FromQueryParameter_ShowsOnlyThatTheme()
    {
        _state.Set([Item("Matrix", "Book", 1), Item("Zion", "Character", 2)]);

        var cut = RenderPage(filter: "Zion");

        Assert.Contains("THEME: Zion", cut.Markup);
        Assert.DoesNotContain("THEME: Matrix", cut.Markup);
    }

    [Fact]
    public void ActiveButton_HasActiveCssClass()
    {
        _state.Set([Item("Matrix", "Book", 1)]);

        var cut = RenderPage(filter: "Matrix");

        var button = cut.FindAll("button").Single(b => b.TextContent.Contains("Matrix"));
        Assert.Contains("active", button.GetAttribute("class"));
    }

    [Fact]
    public void ClickingAllButton_NavigatesToBareThemeRoute()
    {
        _state.Set([Item("Matrix", "Book", 1)]);
        var cut = RenderPage(filter: "Matrix");
        var navigation = Services.GetRequiredService<NavigationManager>();

        cut.FindAll("button").Single(b => b.TextContent.Trim().StartsWith("All")).Click();

        Assert.EndsWith("/hacman/app/theme", navigation.Uri);
    }

    [Fact]
    public void ClickingThemeButton_NavigatesWithFilterQueryParam()
    {
        _state.Set([Item("Matrix", "Book", 1), Item("Zion", "Book", 2)]);
        var cut = RenderPage();
        var navigation = Services.GetRequiredService<NavigationManager>();

        cut.FindAll("button").Single(b => b.TextContent.Contains("Zion")).Click();

        Assert.Contains("/hacman/app/theme?filter=Zion", navigation.Uri);
    }

    [Fact]
    public void ChangingFilterParameter_ToEmpty_ResetsToAll()
    {
        _state.Set([Item("Matrix", "Book", 1), Item("Zion", "Book", 2)]);
        var cut = RenderPage(filter: "Zion");
        Assert.DoesNotContain("THEME: Matrix", cut.Markup);

        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(navigation.GetUriWithQueryParameter("Filter", (string?)null));

        cut.WaitForAssertion(() => Assert.Contains("THEME: Matrix", cut.Markup));
        Assert.Contains("THEME: Zion", cut.Markup);
    }

    [Fact]
    public void RendersGalleryCard_ForEachItemInSubGroup()
    {
        _state.Set([Item("Matrix", "Book", 1), Item("Matrix", "Book", 2)]);

        var cut = RenderPage();

        // Both galleries render as cards under the same THEME/TYPE group.
        Assert.Contains("Gallery 1", cut.Markup);
        Assert.Contains("Gallery 2", cut.Markup);
    }

    [Fact]
    public void RefreshesAutomatically_WhenUserGalleriesStateChanges()
    {
        _state.Set(Array.Empty<UserGallery>());
        var cut = RenderPage();
        Assert.DoesNotContain("THEME:", cut.Markup);

        cut.InvokeAsync(() => _state.Set([Item("Matrix", "Book", 1)]));

        cut.WaitForAssertion(() => Assert.Contains("THEME: Matrix", cut.Markup));
    }
}
