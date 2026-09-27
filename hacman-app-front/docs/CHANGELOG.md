# Changelog

All notable frontend changes to `hacman-app-front` are documented here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project does not yet follow strict [Semantic Versioning](https://semver.org/) release tagging, so entries are grouped by date/theme rather than a single authoritative version number. The current runtime version is tracked in `appsettings.json` → `App:Version`.

## [Unreleased]

## 2.2.0 — 2026-09-25

### Added
- **Achievements page** (`Components/Pages/Achievements.razor`): category filter row, global completion bar, and
  per-achievement progress rows for the new backend achievements feature (100 achievements across 10 categories).
  Backed by a single `GET /api/v1/achievements/me` call via the new `IAchievementsApiService` (mirrors
  `IScoresApiService`), server-side joined so the page needs one round-trip, not two. Reuses the existing
  `matrix-*` CSS classes — no new styling framework, no locked/blurred state (zero progress renders the same way
  as any other progress, just at 0%, matching `Scores.razor`'s existing convention). Added the `Achievements`
  `NavLink` to `Components/Layout/NavMenu.razor`.

### Fixed
- **A real auth bug surfaced by the new page**: `/achievements` showed "0% COMPLETE (0 / 0 LEVELS)" on the deployed
  test environment because `UserBootstrapService`'s localStorage fast-path never seeded
  `UserSessionState.AccessToken`, leaving any circuit whose first `HttpClient` build happened after that fast path
  permanently unauthenticated (`HttpContext`, needed to read the `accessToken` cookie, is only available during a
  circuit's first render). `MainLayout` now calls `ApiClientFactory.ResolveAccessToken()` unconditionally before the
  bootstrap runs, regardless of which bootstrap path executes — a general fix benefiting all four
  `HacmanApiClientFactory`-backed services, not an achievements-only patch. Covered by a new `MainLayoutTest` (bUnit).

### Changed
- Bumped `App:Version` to `2.2.0` (`appsettings.json`).

## 2.1.1 — 2026-09-14

### Fixed
- Gallery zone/type matching (`GalleryTypePage`, `Home.razor` zone cards, `GalleryRouteResolver`) now splits each `HacmanDictionary:{Book,Character,Cosplay}GroupName` config value on `,` and matches a gallery's `gallery_type_enum` value against any of the resulting aliases, instead of a single literal string. Production's `gallery_type_enum` now carries literal `Book`/`Character`/`Cosplay` values (added alongside the original `Type 1`–`Type 11` placeholders) while the test seed data still uses `Type 1`/`Type 2`/`Type 3` for the same three zones — without alias support, galleries added under the new literal names were invisible on their tabs and excluded from the home-page zone counts because prod's config still pointed at `Type 1`/`Type 2`/`Type 3`. `appsettings.json`'s `HacmanDictionary` values are unchanged (single alias, test convention); prod's `hacman-front-prod-config` ConfigMap now overrides each with a two-alias list (`Type 1,Book`, etc.), so neither environment needs to change its `galleries.type` data to match the other.
- Home-page zone-card links (`GetBookUrl`/`GetCharacterUrl`/`GetCosplayUrl`) no longer round-trip a `HacmanDictionary` group-name value through `GalleryRouteResolver.ResolveRouteSegment` (which resolves an actual gallery's type name to a route) — they now build their known, fixed `/book`·`/character`·`/cosplay` destinations directly, so a multi-alias config value no longer breaks the link.

## 2.1.0 — 2026-07-05

### Changed
- Repo-wide **code-review pass** (branch `refactor/code-review`): formatting and readability cleanup across the
  service layer (`HacmanApiClientFactory`, `UserBootstrapService`, `ScoresApiService`, `UserApiService`,
  `GalleryRouteResolver`, state/session services), `Program.cs`, `Domain/UserResponse`, the `matrix.css` styles,
  the test project's `.csproj`, and the doc set — no behavior change.
- Bumped `App:Version` to `2.1.0` (`appsettings.json`).
- Moved `CHANGELOG.md` under `docs/` alongside `ARCHITECTURE.md`/`TESTING.md`; polished `README.md`,
  `docs/ARCHITECTURE.md`, and `docs/TESTING.md` for clarity and consistency.

### Fixed
- Home-page zone cards ("Books"/"Characters"/"Cosplay") navigated to the raw configured group name (e.g. `/Type 1`), a dead route since the `Archive` catch-all page was retired — now routed through `GalleryRouteResolver` to `/book`·`/character`·`/cosplay`, matching the FR-6 `Gallery.razor` fix that had missed `Home.razor`. bUnit-tested in `HomeTest`.

### Added
- Pac-Man favicon (`wwwroot/favicon.svg` + regenerated `favicon.png`), matrix-green to match the theme; referenced from `App.razor` (SVG with PNG fallback).
- `docs/ARCHITECTURE.md`, `docs/TESTING.md`, `README.md`, `CHANGELOG.md`, and `LICENSE` (Apache 2.0).

## 2026-07-02 — 2026-07-04 — v2.0.0 (frontend refactor: test net, cleanup, E2E)

### Added
- **Test project `hacman-app-front.Tests`** (xUnit + bUnit + Moq): unit tests for `UserGalleriesState`, `UserApiService`, `ScoresApiService`, `UserGalleryApiService`, `HacmanApiClientFactory`, `UserBootstrapService`, `GalleryRouteResolver`, and the `AuthController`/`UserInfoController` controllers; bUnit component tests for `GalleryCard` and `GalleryTypePage`; a `WebApplicationFactory`-based DI-registration regression test. 99 tests total.
- **E2E project `hacman-app-front.E2E`** (Microsoft.Playwright + xUnit, deliberately excluded from `hacman-app.sln`): 4 smoke specs — anonymous bootstrap (self-started published instance), authenticated gallery render, Book filter + gallery lightbox, and per-difficulty score rankings — the last three running against the live milkyway.test deployment.
- `Components/Shared/GalleryTypePage.razor` — a single parametrized component backing the Book/Character/Cosplay routes.
- `Services/UserBootstrapService.cs` — the three-path user-bootstrap orchestration (localStorage cache → server fetch → JS-interop callback), extracted out of `MainLayout`.
- `Services/HacmanApiClientFactory.cs` — one shared cookie→session-state token resolver and typed `HttpClient` factory, replacing three duplicated registrations.
- `Services/GalleryRouteResolver.cs` — maps a gallery type name back to its `/book`·`/character`·`/cosplay` route.
- `TrustSelfSignedCertificates` config flag to opt into trusting self-signed/private-CA upstream certificates outside Development.

### Changed
- **Fixed a data-leak bug:** `UserGalleriesState` was registered as a **singleton** while holding per-user gallery/unlock data — in Blazor Server this shared one user's state across every connected circuit. Changed to **scoped**.
- Deduplicated the Nebula token-exchange logic shared by `AuthController.Callback`/`Refresh` into one helper.
- `ScoresApiService` now fetches the three difficulties concurrently (`Task.WhenAll`) and uses per-request `HttpRequestMessage` authorization instead of mutating the shared `HttpClient.DefaultRequestHeaders`.
- Collapsed the near-identical `Book`/`Character`/`Cosplay` pages (~160 lines each) into ~10-line shells over `GalleryTypePage`; retired `Archive.razor` by repointing gallery navigation through `GalleryRouteResolver`.
- `MainLayout.razor` slimmed from ~392 to ~145 lines — bootstrap logic moved to `UserBootstrapService`, leaving only rendering and thin delegation.
- Renamed the `auth/` and `domain/` folders (and their namespaces) to PascalCase `Auth/`/`Domain/`, matching `Components/`/`Services/`.
- Application version bumped to `2.0.0`.

### Fixed
- `GalleryCard.Play()` double-encoded the gallery title in the game URL (`Uri.EscapeDataString` followed by `QueryHelpers.AddQueryString`, encoding twice) — the game received a literal `%2520`. Now encoded exactly once.

### Removed
- Security-hygiene cleanup: gated `DangerousAcceptAnyServerCertificateValidator` behind Development/`TrustSelfSignedCertificates` (was applied unconditionally), and removed a debug middleware that logged **all** request headers — including the `Cookie:` header carrying the access token — on every request.
- Verified dead code: `EnsureAuthenticatedMiddleware` (never registered), `UserInfoController.EnsureHacmanUser`/`ExtractExternalAuthIdFromToken` (bootstrap moved client-side), `UserGalleryResponse`, and `UserApiService.GetUserDataFromMe`.

## 2026-06-20 — score UI & navigation polish

### Added
- Per-difficulty score display on gallery/scores views; auto-load scores on first render.
- Static file serving with explicit CSS MIME-type handling; 30s HTTP client timeout for reliable DNS resolution in Docker.

### Changed
- Score handling switched to `ScoreEntryDto`; scores categorized by difficulty (EASY/MEDIUM/HARD).
- Navigation refactors: buttons vs. anchors normalized, `AppBasePath`-aware paths, `%20` space encoding in filter links.

## 2026-06-18 — model clarity

### Changed
- `LevelScore` and gallery models refactored to reference `Gallery`/`User` objects and stable JSON property names for reliable serialization with the backend.
- Private fields standardized to the `_underscore` convention across `Cosplay`/`Home`/`Scores`.

## 2026-06-06 — 2026-06-10 — bootstrap & resources

### Added
- `HacmanResourcesUrl` configuration and image-URL fallback (`missing.jpg`) handling in `GalleryCard`/`Scores`.
- `app.js` client-side bootstrap: caches the bootstrap payload and auth errors in `localStorage`; server-side user loading in `MainLayout` for better SSR.
- Non-blocking reconnect banner for circuit reconnection.
- `MaxRedirectAttempts` configuration for the Nebula login redirect flow.

### Changed
- Domain namespaces moved from `hacman_app_back` to `hacman_app_front`.

## Initial scaffold

### Added
- Blazor Web Server application: Nebula login flow (JWT issued by the Andromeda-backed authentication gateway), `accessToken` cookie handling, gallery browsing by type/theme/subtype, gallery detail with image lightbox, and the `GalleryCard` component.

