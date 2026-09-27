using hacman_app_front.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace hacman_app_front.E2E.Smoke;

/// <summary>
/// First E2E smoke test (FE-1): the app starts and an anonymous visitor lands on the
/// login-required state on the home page, with no backend or database required.
///
/// This asserts on <c>Home.razor</c>'s "ACCESS_RESTRICTED" / "IDENTIFICATION_REQUIRED" block,
/// not <c>MainLayout</c>'s top-bar "[ LOG IN ]" error banner: the home page's anonymous state is
/// driven purely by <c>_userName == "ANONYMOUS"</c> (the field's initial value, unchanged for a
/// genuinely anonymous visitor) and renders identically from the very first static prerender —
/// deterministic and stable. The top-bar banner instead depends on a client-side race between
/// two independent bootstrap paths (server-side <c>UserBootstrapService.LoadUserDataAsync</c>
/// racing the browser-side <c>app.js</c> → <c>MainLayout.ReceiveAuthError</c> JS-interop
/// callback) and was observed, while writing this test, to sometimes never show the error text
/// even after several seconds — not something a smoke test should depend on.
/// </summary>
[Collection(FrontendE2ECollection.Name)]
public class AnonymousUserTest
{
    private readonly WebAppFixture _fixture;

    public AnonymousUserTest(WebAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AnonymousUser_SeesIdentificationRequiredState_OnHomePage()
    {
        var context = await _fixture.NewContextAsync();
        try
        {
            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[console:{msg.Type}] {msg.Text}");
            page.RequestFailed += (_, req) => Console.WriteLine($"[reqfailed] {req.Method} {req.Url} - {req.Failure}");
            page.PageError += (_, err) => Console.WriteLine($"[pageerror] {err}");

            await page.GotoAsync(_fixture.BaseUrl);

            try
            {
                var heading = page.Locator("h1.matrix-glitch[data-text='ACCESS_RESTRICTED']");
                await heading.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

                await Assertions.Expect(page.Locator(".zone-title", new PageLocatorOptions { HasText = "IDENTIFICATION_REQUIRED" }))
                    .ToBeVisibleAsync();
                await Assertions.Expect(page.Locator("button", new PageLocatorOptions { HasText = "INITIATE_LOGIN_SEQUENCE" }))
                    .ToBeVisibleAsync();
            }
            catch
            {
                Console.WriteLine("=== PAGE HTML ON FAILURE ===");
                Console.WriteLine(await page.ContentAsync());
                throw;
            }
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
