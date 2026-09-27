using System.Text.Json;
using hacman_app_front.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace hacman_app_front.E2E.Authenticated;

/// <summary>
/// Fourth E2E test (FE-4): opening a gallery's score rankings renders the three per-difficulty
/// tables, against the real milkyway.test deployment (see <see cref="AuthenticatedStackFixture"/>).
///
/// The <c>[ SCORE_RANKINGS ]</c> toggle on <c>Gallery.razor</c> is <b>not</b> lock-gated (unlike a
/// card's VIEW/RANKINGS buttons), so no unlock is needed — but the three tables only render when
/// the gallery actually has scores; an empty result shows "NO DATA RECORDED IN THE ARCHIVES."
/// instead. The deployment's shared database is seeded with scores for every gallery (10 per
/// difficulty), so any bootstrapped gallery id works. The detail page is reached by deep-link for
/// the same reason as FE-3 (a freshly-bootstrapped user's galleries are all locked, so a card's
/// VIEW is disabled).
/// </summary>
[Collection(AuthenticatedStackCollection.Name)]
public class GalleryScoresFlowTest : AuthenticatedFlowTestBase
{
    public GalleryScoresFlowTest(AuthenticatedStackFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GalleryDetail_ScoreRankings_RendersThreeDifficultyTables()
    {
        var externalAuthId = TestUsers.NewExternalAuthId();
        var (context, page) = await NewAuthenticatedPageAsync(externalAuthId);
        try
        {
            var galleriesJson = await BootstrapAndReadGalleriesJsonAsync(page);
            var galleryId = FirstGalleryId(galleriesJson);

            await page.GotoAsync($"{Fixture.BaseUrl}gallery/{galleryId}");

            // The rankings toggle appears once the gallery resolves from state; it's always present
            // (not lock-gated) and starts collapsed.
            var rankingsToggle = page.Locator("button", new() { HasTextString = "SCORE_RANKINGS" });
            await rankingsToggle.WaitForAsync(new() { Timeout = 20_000 });
            await rankingsToggle.ClickAsync();

            // One column per difficulty, headed HARD / MEDIUM / EASY (the page's fixed render order).
            var difficultyColumns = page.Locator(".difficulty-column");
            await Assertions.Expect(difficultyColumns).ToHaveCountAsync(3);
            await Assertions.Expect(page.Locator(".difficulty-header"))
                .ToHaveTextAsync(new[] { "HARD", "MEDIUM", "EASY" });

            // Every difficulty is seeded with 10 scores → 30 data rows total (proves the tables are
            // populated, not showing the empty-state "No scores" placeholder).
            await Assertions.Expect(page.Locator(".difficulty-table tbody tr")).ToHaveCountAsync(30);

            // The toggle flipped to the collapse label.
            await Assertions.Expect(page.Locator("button", new() { HasTextString = "HIDE_RANKINGS" }))
                .ToBeVisibleAsync();
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

    /// <summary>Returns the first gallery id from the cached bootstrap payload (every gallery is score-seeded).</summary>
    private static Guid FirstGalleryId(string galleriesJson)
    {
        using var document = JsonDocument.Parse(galleriesJson);
        foreach (var element in document.RootElement.EnumerateArray())
        {
            return element.GetProperty("gallery").GetProperty("id").GetGuid();
        }

        throw new InvalidOperationException("No galleries found in the bootstrapped payload.");
    }
}
