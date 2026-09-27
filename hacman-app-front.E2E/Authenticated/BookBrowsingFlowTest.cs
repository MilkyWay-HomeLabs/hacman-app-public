using System.Text.Json;
using System.Text.RegularExpressions;
using hacman_app_front.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace hacman_app_front.E2E.Authenticated;

/// <summary>
/// Third E2E test (FE-3): the core authenticated browsing flow against the real milkyway.test
/// deployment (see <see cref="AuthenticatedStackFixture"/>) — the Book page's subtype filter
/// buttons, then a gallery detail page's image lightbox (open → next → close).
///
/// Two design notes:
/// <list type="bullet">
///   <item>The Book type ("Type 1" per <c>HacmanDictionary:BookGroupName</c>) always has 30 seeded
///   galleries across 3 subtypes (Subtype 1/2/3), each with <c>imageCount = 10</c> — so the filter
///   has real groups to narrow and the lightbox has ≥2 images to advance through.</item>
///   <item>The gallery detail page is reached by <b>deep-link</b> (<c>/gallery/{id}</c> is a
///   first-class route), not by clicking a card's <c>[ VIEW ]</c> button. A freshly-bootstrapped
///   user has every gallery <em>locked</em>, and <c>GalleryCard</c> disables VIEW while locked
///   (<c>disabled="@(!Item.IsUnlocked)"</c>) — unlocking one would require a direct DB write, which
///   the project rules forbid. The detail page and lightbox render purely from
///   <c>UserGalleriesState</c> regardless of lock state, so deep-linking exercises them faithfully.</item>
/// </list>
/// </summary>
[Collection(AuthenticatedStackCollection.Name)]
public class BookBrowsingFlowTest : AuthenticatedFlowTestBase
{
    public BookBrowsingFlowTest(AuthenticatedStackFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task BookPage_SubtypeFilterButton_NarrowsToSelectedSubtype()
    {
        var externalAuthId = TestUsers.NewExternalAuthId();
        var (context, page) = await NewAuthenticatedPageAsync(externalAuthId);
        try
        {
            await page.GotoAsync($"{Fixture.BaseUrl}book");

            await Assertions.Expect(page.Locator("h3", new() { HasTextString = "Book Levels" }))
                .ToBeVisibleAsync();

            // Filter buttons only appear once the bootstrap has loaded the user's galleries into
            // UserGalleriesState — wait for the first subtype button before asserting.
            var subtype1Button = page.Locator("button", new() { HasTextString = "Subtype 1" });
            await subtype1Button.WaitForAsync(new() { Timeout = 20_000 });

            // Default state: "All" filter active, one section per subtype (3 for the Book type).
            var sectionHeaders = page.Locator("section.mb-4 h4");
            await Assertions.Expect(sectionHeaders).ToHaveCountAsync(3);

            var allButton = page.Locator("button", new() { HasTextString = "All (" });
            await Assertions.Expect(allButton).ToHaveClassAsync(new Regex(@"\bactive\b"));

            // Click the "Subtype 1" filter → URL carries the filter, only that section remains, and
            // the clicked button becomes the active one.
            await subtype1Button.ClickAsync();

            await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"filter=Subtype"));
            await Assertions.Expect(sectionHeaders).ToHaveCountAsync(1);
            await Assertions.Expect(sectionHeaders).ToHaveTextAsync(new Regex(@"^Subtype 1$"));
            await Assertions.Expect(subtype1Button).ToHaveClassAsync(new Regex(@"\bactive\b"));
            await Assertions.Expect(allButton).Not.ToHaveClassAsync(new Regex(@"\bactive\b"));
        }
        catch
        {
            await DumpPageAsync(page);
            throw;
        }
        finally
        {
            await context.CloseAsync();
            await TestUsers.CleanupAsync(externalAuthId);
        }
    }

    [Fact]
    public async Task GalleryDetail_Lightbox_OpensAdvancesAndCloses()
    {
        var externalAuthId = TestUsers.NewExternalAuthId();
        var (context, page) = await NewAuthenticatedPageAsync(externalAuthId);
        try
        {
            // Land on Home first so the bootstrap runs and caches the galleries, then pick a real
            // Book-type gallery id (imageCount 10) to deep-link into.
            var galleriesJson = await BootstrapAndReadGalleriesJsonAsync(page);
            var (galleryId, imageCount) = PickBookGallery(galleriesJson);

            await page.GotoAsync($"{Fixture.BaseUrl}gallery/{galleryId}");

            // The detail grid renders one thumbnail per image once the gallery resolves from state.
            var thumbnails = page.Locator(".matrix-gallery-thumb");
            await thumbnails.First.WaitForAsync(new() { Timeout = 20_000 });
            await Assertions.Expect(thumbnails).ToHaveCountAsync(imageCount);

            var overlay = page.Locator(".matrix-lightbox-overlay");
            var info = page.Locator(".matrix-lightbox-info");

            // Open on the first image.
            await thumbnails.First.ClickAsync();
            await Assertions.Expect(overlay).ToBeVisibleAsync();
            await Assertions.Expect(info).ToContainTextAsync($"IMAGE_SEQUENCE: 1 / {imageCount}");

            // Advance to the second (the ▶ button; stopPropagation keeps the overlay open).
            await page.Locator(".matrix-lightbox-btn", new() { HasTextString = "▶" }).ClickAsync();
            await Assertions.Expect(info).ToContainTextAsync($"IMAGE_SEQUENCE: 2 / {imageCount}");

            // Close.
            await page.Locator(".matrix-lightbox-close").ClickAsync();
            await Assertions.Expect(overlay).Not.ToBeVisibleAsync();
        }
        catch
        {
            await DumpPageAsync(page);
            throw;
        }
        finally
        {
            await context.CloseAsync();
            await TestUsers.CleanupAsync(externalAuthId);
        }
    }

    /// <summary>Picks the first Book-type ("Type 1") gallery with ≥2 images from the cached bootstrap payload.</summary>
    private static (Guid Id, int ImageCount) PickBookGallery(string galleriesJson)
    {
        using var document = JsonDocument.Parse(galleriesJson);
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var gallery = element.GetProperty("gallery");
            var imageCount = gallery.GetProperty("imageCount").GetInt32();
            if (gallery.GetProperty("type").GetProperty("name").GetString() == "Type 1" && imageCount >= 2)
            {
                return (gallery.GetProperty("id").GetGuid(), imageCount);
            }
        }

        throw new InvalidOperationException(
            "No Book-type ('Type 1') gallery with >=2 images found in the bootstrapped galleries.");
    }
}
