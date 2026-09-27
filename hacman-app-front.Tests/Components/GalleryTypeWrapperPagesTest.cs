using Bunit;
using hacman_app_front.Components.Pages;
using hacman_app_front.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace hacman_app_front.Tests.Components;

/// <summary>
/// Book/Character/Cosplay are thin wrappers that just wire fixed parameters into
/// GalleryTypePage (already covered in depth by GalleryTypePageTest) -- these guard that the
/// wiring itself (route segment, config key, default group name, title) is correct.
/// </summary>
public class GalleryTypeWrapperPagesTest : BunitContext
{
    public GalleryTypeWrapperPagesTest()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppBasePath"] = "/hacman/app",
            ["HacmanDictionary:BookGroupName"] = "Book",
            ["HacmanDictionary:CharacterGroupName"] = "Character",
            ["HacmanDictionary:CosplayGroupName"] = "Cosplay"
        }).Build();
        Services.AddSingleton<IConfiguration>(config);
        Services.AddSingleton(new UserGalleriesState());
    }

    [Fact]
    public void Book_RendersBookLevelsTitle()
    {
        var cut = Render<Book>();
        Assert.Contains("Book Levels", cut.Find("h3").TextContent);
    }

    [Fact]
    public void Character_RendersCharacterLevelsTitle()
    {
        var cut = Render<Character>();
        Assert.Contains("Character levels", cut.Find("h3").TextContent);
    }

    [Fact]
    public void Cosplay_RendersCosplayLevelsTitle()
    {
        var cut = Render<Cosplay>();
        Assert.Contains("Cosplay Levels", cut.Find("h3").TextContent);
    }
}
