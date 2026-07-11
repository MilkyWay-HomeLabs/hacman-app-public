# Testing

How the frontend is tested and how to run each suite. For the architecture the tests exercise, see [
`ARCHITECTURE.md`](ARCHITECTURE.md).

## Contents

- [Test projects & frameworks](#test-projects--frameworks)
- [Running the unit & component tests](#running-the-unit--component-tests)
- [Running the E2E tests](#running-the-e2e-tests)
- [Fixture conventions](#fixture-conventions)
- [Current coverage snapshot](#current-coverage-snapshot)
- [Writing a new test](#writing-a-new-test)

## Test projects & frameworks

| Project                  | Frameworks                                                     | In `hacman-app.sln`?    | Purpose                                                        |
|--------------------------|----------------------------------------------------------------|-------------------------|----------------------------------------------------------------|
| `hacman-app-front.Tests` | xUnit + [bUnit](https://bunit.dev/) + Moq                      | ✅ yes                   | Unit tests (services, controllers) and component tests (bUnit) |
| `hacman-app-front.E2E`   | [Microsoft.Playwright](https://playwright.dev/dotnet/) + xUnit | ❌ **no** (deliberately) | Browser end-to-end smoke tests                                 |

`hacman-app-front.E2E` is intentionally **excluded** from `hacman-app.sln`: it needs the Playwright browser binaries and
starts a real frontend process (or targets a live deployment), so it must stay out of the plain
`dotnet test ./hacman-app.sln` used elsewhere. Run it with its own explicit project path.

## Running the unit & component tests

```bash
# Just the frontend unit/component tests
dotnet test ./hacman-app-front.Tests/hacman-app-front.Tests.csproj

# As part of the whole solution (backend + frontend, unit-only)
dotnet test ./hacman-app.sln --filter "TestCategory!=Integration"
```

The frontend tests carry no `[TestCategory("Integration")]` marker, so they always run under the standard unit filter
used across the repo.

**bUnit component tests** inherit `BunitContext` (bUnit v2 API — v1's `TestContext` is gone), register an in-memory
`IConfiguration`, and assert navigation through bUnit's built-in fake `NavigationManager`. See
`Components/GalleryCardTest.cs` / `Components/GalleryTypePageTest.cs`.

**DI-registration tests** (`Services/ServiceRegistrationTest.cs`) boot the real composition root via
`WebApplicationFactory<Program>` (this is why `Program.cs` ends with a `public partial class Program` marker — top-level
statements otherwise generate an internal `Program`). They guard the scoped-vs-singleton lifetime of the per-user state
services.

## Running the E2E tests

### One-time setup — install the Playwright browser binaries

```bash
# If pwsh is available (generated into bin/ after the first build):
pwsh hacman-app-front.E2E/bin/Release/net10.0/playwright.ps1 install --with-deps chromium

# No-PowerShell fallback:
dotnet tool install --global Microsoft.Playwright.CLI
playwright install --with-deps chromium
```

### Anonymous smoke test — local self-start

The `WebAppFixture` launches an already-**published** frontend on its own port. No backend/DB needed (the anonymous path
fails at the frontend's own token check before reaching the Hacman API). Publish first, every time the frontend source
changes:

```bash
dotnet publish ./hacman-app-front/hacman-app-front.csproj -c Release
dotnet test ./hacman-app-front.E2E/hacman-app-front.E2E.csproj -c Release --filter "FullyQualifiedName~Smoke"
```

> **Publish, not build:** a plain `dotnet build` output's Static Web Assets manifest resolves compressed (`.br`/`.gz`)
> asset variants that don't physically exist outside the publish output, so every `<script>`/`<link>` 200s with
`Content-Length: 0` and the Blazor circuit silently never boots.

### Authenticated tests — against the live deployment

The `Authenticated/` specs (`AuthenticatedStackFixture`) run **only** against the real running deployment — they cannot
self-start on localhost. The reason is fundamental: the `accessToken` is an HTTP-only JWT cookie **domain-scoped
to `milkyway.test`**, and a browser only sends a cookie to the host it is scoped to, so the page must be served from
`milkyway.test`. The fixture injects the cookie at the browser level (`BrowserContext.AddCookiesAsync`) and defaults to
`https://milkyway.test/hacman/app/`.

```bash
# Requires the milkyway.test test deployment running (hacman-app-{front,back}-test + milky-test-postgres)
dotnet test ./hacman-app-front.E2E/hacman-app-front.E2E.csproj -c Release --filter "FullyQualifiedName~Authenticated"
```

Point at a different environment with `HACMAN_E2E_BASE_URL`. `HACMAN_E2E_IGNORE_HTTPS_ERRORS` defaults to **true** here
because milkyway.test sits behind Traefik with a private-CA certificate.

> **Deployment prerequisite (surfaced by the score-rankings spec):** the frontend-test container needs
`TrustSelfSignedCertificates=true`. The score fetch runs **server-side** on the Blazor circuit (not in the browser), so
> it is the first E2E to hit the TLS gate — without the flag, every difficulty fetch fails validation with `UntrustedRoot`
> and the tables render empty.

### Environment variables

| Variable                         | Default                                            | Effect                                                 |
|----------------------------------|----------------------------------------------------|--------------------------------------------------------|
| `HACMAN_E2E_BASE_URL`            | (self-start) / `https://milkyway.test/hacman/app/` | Target an existing deployment instead of self-starting |
| `HACMAN_E2E_IGNORE_HTTPS_ERRORS` | `false` (smoke) / `true` (authenticated)           | Ignore the self-signed cert on milkyway.test           |

## Fixture conventions

- **Unit HTTP mocking:** `TestHelpers/FakeHttpMessageHandler.cs` returns canned responses and records every request (
  method/URL/body) for assertions. Reused across the API-service tests.
- **E2E self-start:** `Infrastructure/WebAppFixture.cs` — launches the published frontend, shares a headless Chromium (
  with `--no-sandbox --disable-dev-shm-usage` for this container), overrides `AppBasePath=/`. Environment quirks are
  documented as comments in that file.
- **E2E authenticated:** `Infrastructure/AuthenticatedStackFixture.cs` + `TestJwt.cs` (hand-built unsigned JWT — the
  backend disables signature validation, checking only `exp`) + `TestUsers.cs` (`NewExternalAuthId()` /
  `CleanupAsync()`). `Authenticated/AuthenticatedFlowTestBase.cs` holds the shared per-test helpers (
  `NewAuthenticatedPageAsync`, `BootstrapAndReadGalleriesJsonAsync`, `DumpPageAsync`).
- Authenticated specs create a `test_e2e_*` user via the normal bootstrap and delete it (cascade removes seeded
  galleries) in `finally`.

## Current coverage snapshot

| Suite                                     | Count  | What it covers                                                                                                                                                                                                |
|-------------------------------------------|--------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Unit/component (`hacman-app-front.Tests`) | **99** | State (`UserGalleriesState`), API services, `HacmanApiClientFactory`, `UserBootstrapService`, `GalleryRouteResolver`, `AuthController`/`UserInfoController`, `GalleryCard`/`GalleryTypePage`, DI registration |
| E2E (`hacman-app-front.E2E`)              | **4**  | Anonymous bootstrap; authenticated gallery render; Book filter + gallery lightbox; per-difficulty score rankings                                                                                              |

The E2E scope is intentionally kept at ~4 smoke-level specs — a safety net, not a full UI-test suite.

## Writing a new test

- **Match the existing pattern** for the layer: `FakeHttpMessageHandler` for API-client tests, `BunitContext` for
  components, `WebApplicationFactory<Program>` when you need the real DI graph.
- **Assert unconditionally** — do not wrap assertions in `if (x != null)`; construct your own fixture data instead of
  depending on pre-existing state.
- **E2E:** reach authenticated pages by deep-link where a freshly-bootstrapped user's cards would be locked (all
  galleries start locked, and `GalleryCard` disables VIEW/RANKINGS while locked). Always clean up `test_e2e_*` users in
  `finally`.
- Keep the E2E suite smoke-level; prefer a unit or bUnit test when the behavior can be exercised without a browser.

---

For the backend's testing guide, see [`../../hacman-app-back/docs/TESTING.md`](../../hacman-app-back/docs/TESTING.md).
