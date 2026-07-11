using hacman_app_front.E2E.Infrastructure;
using Microsoft.Playwright;

namespace hacman_app_front.E2E.Authenticated;

/// <summary>
/// Shared setup for the authenticated E2E flows (FE-2/FE-3/FE-4) that run against the milkyway.test
/// deployment via <see cref="AuthenticatedStackFixture"/>: minting an authenticated browser page,
/// waiting out the client-side bootstrap, and dumping page HTML on failure. Each test still owns
/// its own throwaway user (created via bootstrap, deleted in <c>finally</c>) so the flows stay
/// independent.
/// </summary>
public abstract class AuthenticatedFlowTestBase
{
    protected AuthenticatedStackFixture Fixture { get; }

    protected AuthenticatedFlowTestBase(AuthenticatedStackFixture fixture)
    {
        Fixture = fixture;
    }

    /// <summary>
    /// Creates a browser context + page carrying a freshly-minted <c>accessToken</c> cookie for
    /// <paramref name="externalAuthId"/>, with console / request-failure / page-error tracing wired
    /// up so a failing run leaves diagnostics in the test output.
    /// </summary>
    protected async Task<(IBrowserContext Context, IPage Page)> NewAuthenticatedPageAsync(string externalAuthId)
    {
        var email = $"{externalAuthId}@example.com";
        var accessToken = TestJwt.CreateAccessToken(externalAuthId, email);

        var context = await Fixture.NewAuthenticatedContextAsync(accessToken);
        var page = await context.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[console:{msg.Type}] {msg.Text}");
        page.RequestFailed += (_, req) => Console.WriteLine($"[reqfailed] {req.Method} {req.Url} - {req.Failure}");
        page.PageError += (_, err) => Console.WriteLine($"[pageerror] {err}");
        return (context, page);
    }

    /// <summary>
    /// Navigates to Home and waits until <c>app.js</c> has bootstrapped and cached the user's
    /// galleries, returning the raw <c>hacmanUserGalleries</c> localStorage JSON (full gallery ids,
    /// image counts, type/subtype) for tests that need to deep-link into a specific gallery.
    /// </summary>
    protected async Task<string> BootstrapAndReadGalleriesJsonAsync(IPage page)
    {
        await page.GotoAsync(Fixture.BaseUrl);
        await page.WaitForFunctionAsync(
            "() => { const v = localStorage.getItem('hacmanUserGalleries'); return v && JSON.parse(v).length > 0; }",
            null,
            new PageWaitForFunctionOptions { Timeout = 20_000 });

        return await page.EvaluateAsync<string>("() => localStorage.getItem('hacmanUserGalleries')");
    }

    protected static async Task DumpPageAsync(IPage page)
    {
        Console.WriteLine("=== PAGE HTML ON FAILURE ===");
        Console.WriteLine(await page.ContentAsync());
    }
}
