using hacman_app_front.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace hacman_app_front.E2E.Authenticated;

/// <summary>
/// Second E2E test (FE-2): an authenticated visitor's Home page renders their galleries after
/// bootstrap, against the real running Hacman deployment at <c>milkyway.test</c> (frontend + API
/// same-origin behind Traefik, backed by the shared <c>milky-test-postgres</c> instance) — no
/// mocking, no Nebula login round-trip. See <see cref="AuthenticatedStackFixture"/> for why this
/// must target the milkyway.test host rather than a localhost self-start (the httpOnly
/// <c>accessToken</c> cookie is domain-scoped to milkyway.test).
///
/// Authentication is injected directly as the httpOnly <c>accessToken</c> cookie (see
/// <see cref="TestJwt"/> for why a real signature isn't needed) rather than driven through the
/// browser, since a real login redirects to Nebula and can't be automated here. This is the first
/// bootstrap run for a brand-new external auth id, so the backend creates the Hacman user and
/// seeds one (locked) <c>UserGallery</c> row per existing gallery — exactly what a first-ever
/// real login does too.
/// </summary>
[Collection(AuthenticatedStackCollection.Name)]
public class GalleriesRenderOnHomeTest : AuthenticatedFlowTestBase
{
    public GalleriesRenderOnHomeTest(AuthenticatedStackFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task AuthenticatedUser_SeesUndiscoveredGalleries_OnHomePage()
    {
        var externalAuthId = TestUsers.NewExternalAuthId();
        var (context, page) = await NewAuthenticatedPageAsync(externalAuthId);
        try
        {
            await page.GotoAsync(Fixture.BaseUrl);

            var undiscoveredHeading = page.Locator("h4", new() { HasTextString = "PRIORITY_TARGETS: UNDISCOVERED_LEVELS" });
            await undiscoveredHeading.WaitForAsync(new() { Timeout = 20_000 });

            var galleryCards = page.Locator(".matrix-card");
            Assert.True(await galleryCards.CountAsync() > 0, "Expected at least one gallery card to render.");

            await Assertions.Expect(page.Locator("h1.matrix-glitch[data-text='ACCESS_RESTRICTED']")).Not.ToBeVisibleAsync();
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
}
