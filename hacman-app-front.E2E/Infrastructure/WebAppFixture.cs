using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;

namespace hacman_app_front.E2E.Infrastructure;

/// <summary>
/// Boots the Blazor Server frontend for the duration of an E2E test collection and launches one
/// shared headless Chromium instance. Two modes, selected by the <see cref="BaseUrlEnvVar"/>
/// environment variable:
/// <list type="bullet">
///   <item>
///     Set — assumes a frontend is already running there (e.g. a developer's own
///     <c>dotnet run</c>, or a CI step that started the full stack) and this fixture does not
///     manage its lifecycle.
///   </item>
///   <item>
///     Not set — launches the already-*published* <c>hacman-app-front.dll</c> directly (not
///     <c>dotnet run</c>, which spawns a child process that survives killing the parent, and not
///     the plain <c>dotnet build</c> output — see <see cref="ResolveFrontendDll"/>) on a
///     dedicated port, with <c>MaxRedirectAttempts=0</c> so the anonymous bootstrap path is
///     deterministic: the very first failed token check trips the ERROR/[ LOG IN ] UI instead of
///     navigating away to the (unreachable in tests) Nebula login URL. See
///     <c>wwwroot/js/app.js</c>'s <c>redirectToLogin()</c>.
///   </item>
/// </list>
///
/// Requires the Playwright browser binaries to be installed once per machine — see
/// CLAUDE.md's "Frontend E2E tests" section under Commands for the exact command
/// (a <c>pwsh</c> script, or a no-PowerShell fallback).
/// </summary>
public sealed class WebAppFixture : IAsyncLifetime
{
    private const string BaseUrlEnvVar = "HACMAN_E2E_BASE_URL";
    private const string IgnoreHttpsErrorsEnvVar = "HACMAN_E2E_IGNORE_HTTPS_ERRORS";
    private const int TestPort = 5299;

    private Process? _process;
    private IPlaywright? _playwright;
    private bool _ignoreHttpsErrors;

    public string BaseUrl { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    /// <summary>
    /// Creates a browser context pre-configured for this run — in particular honoring
    /// <see cref="IgnoreHttpsErrorsEnvVar"/>, which every test should go through instead of
    /// calling <c>Browser.NewContextAsync()</c> directly. Needed against the real
    /// milkyway.test deployments (dev: https://milkyway.test/dev/hacman/app/, prod:
    /// https://milkyway.test/hacman/app/), which sit behind Traefik with a self-signed/private-CA
    /// certificate — the same certificate the app's own backend calls trust via
    /// TrustSelfSignedCertificates (see CLAUDE.md's Frontend config section). Not needed for the
    /// local self-start mode (plain http on 127.0.0.1).
    /// </summary>
    public Task<IBrowserContext> NewContextAsync() =>
        Browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = _ignoreHttpsErrors });

    public async Task InitializeAsync()
    {
        var configuredUrl = Environment.GetEnvironmentVariable(BaseUrlEnvVar);
        BaseUrl = configuredUrl ?? await StartFrontendAsync();
        _ignoreHttpsErrors = bool.TryParse(Environment.GetEnvironmentVariable(IgnoreHttpsErrorsEnvVar), out var v) && v;

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            // Chromium's setuid sandbox needs unprivileged user namespaces, which many
            // containerized/CI environments (incl. this one) don't grant — without this the
            // renderer process fails to start and every page silently never executes JS
            // (network requests still succeed since those happen in the browser process).
            Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" },
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process?.Dispose();
    }

    private async Task<string> StartFrontendAsync()
    {
        var dllPath = ResolveFrontendDll();
        var baseUrl = $"http://127.0.0.1:{TestPort}";

        var startInfo = new ProcessStartInfo("dotnet", $"\"{dllPath}\"")
        {
            // Without this, the content root defaults to *this test process's* working
            // directory, not the app's own folder — appsettings.json then silently fails to
            // load (wrong path), and every config-dependent default (AppBasePath, etc.) falls
            // back to Program.cs's hardcoded fallback instead of the real configured value.
            WorkingDirectory = Path.GetDirectoryName(dllPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.Environment["ASPNETCORE_URLS"] = baseUrl;
        startInfo.Environment["MaxRedirectAttempts"] = "0";
        // appsettings.json (Production, which is what a plain `dotnet <dll>` loads with no
        // ASPNETCORE_ENVIRONMENT set) configures AppBasePath="/hacman/app" and the app calls
        // app.UsePathBase(appBasePath) — so without this override every request to BaseUrl's
        // root 404s, since the pipeline only strips and routes requests under that prefix.
        startInfo.Environment["AppBasePath"] = "/";

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start frontend process from '{dllPath}'.");
        // Must actively drain these: RedirectStandardOutput/Error back the child's console with a
        // finite OS pipe (64KB on Linux). If nothing reads it, the child's next write() blocks
        // once the pipe fills — which can silently stall request handling entirely. See
        // AuthenticatedStackFixture.DrainOutput for the full story of how this was found (a real
        // hang, not a networking issue, discovered while writing FE-2 against the heavier-logging
        // backend process — ported the same fix here since the risk is identical, just less
        // likely to trigger given how little this frontend process logs).
        _ = Task.Run(async () => { try { await _process.StandardOutput.ReadToEndAsync(); } catch { /* process exited */ } });
        _ = Task.Run(async () => { try { await _process.StandardError.ReadToEndAsync(); } catch { /* process exited */ } });

        await WaitForReadyAsync(baseUrl);
        return baseUrl;
    }

    private static string ResolveFrontendDll()
    {
        var solutionRoot = FindSolutionRoot();
        var frontDir = Path.Combine(solutionRoot, "hacman-app-front");

        // Deliberately the `publish` output, not the plain `bin/{config}/net10.0` build output:
        // a plain `dotnet build` leaves the Static Web Assets manifest pointing at compressed
        // (.br/.gz) representations that don't physically exist yet outside of `publish/wwwroot`.
        // MapStaticAssets still returns 200 for every asset in that case, but with
        // Content-Length: 0 — so every <script>/<link> silently loads as empty content and the
        // Blazor circuit / app.js bootstrap never runs, with no error surfaced anywhere (found by
        // hand while first wiring this fixture up: curl showed 0-byte bodies for real files that
        // are 11KB+ on disk).
        foreach (var configuration in new[] { "Release", "Debug" })
        {
            var candidate = Path.Combine(frontDir, "bin", configuration, "net10.0", "publish", "hacman-app-front.dll");
            if (File.Exists(candidate)) return candidate;
        }

        throw new InvalidOperationException(
            $"Published hacman-app-front.dll not found under {frontDir}/bin/{{Release,Debug}}/net10.0/publish. " +
            "Publish the frontend first: dotnet publish ./hacman-app-front/hacman-app-front.csproj -c Release " +
            $"(or set {BaseUrlEnvVar} to point at an already-running instance).");
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "hacman-app.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate hacman-app.sln above " + AppContext.BaseDirectory);
    }

    private static async Task WaitForReadyAsync(string baseUrl)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await http.GetAsync(baseUrl);
                return; // any response (even non-2xx) means Kestrel is up and routing requests.
            }
            catch
            {
                await Task.Delay(500);
            }
        }

        throw new TimeoutException($"Frontend at {baseUrl} did not become ready within 30s.");
    }
}

[CollectionDefinition(Name)]
public sealed class FrontendE2ECollection : ICollectionFixture<WebAppFixture>
{
    public const string Name = "Frontend E2E";
}
