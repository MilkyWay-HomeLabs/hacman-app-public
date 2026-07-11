# Changelog

All notable backend changes to `hacman-app-back` are documented here. Format
follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project does not yet follow
strict [Semantic Versioning](https://semver.org/) release tagging, so entries are grouped by date/theme rather than a
single authoritative version number. The current runtime version is tracked in `appsettings.json` → `App:Version`.

## [Unreleased]

## 2.1.0 — 2026-07-05

### Changed

- Repo-wide **code-review pass** (branch `refactor/code-review`): formatting and readability cleanup across every
  bounded context (`Program.cs`, and the Identity, GalleryCatalog, PlayerProgress, Scoring, and LevelTemplate modules)
  plus `SharedKernel`, the test suite, and the doc set — no behavior change.
- Bumped `App:Version` / `InformationalVersion` to `2.1.0` (`appsettings.json`).
- Moved `CONTRIBUTING.md` under `docs/`, alongside the rest of the documentation set; polished `README.md`,
  `docs/ARCHITECTURE.md`, `docs/API.md`, and `docs/TESTING.md` for clarity and consistency.

### Removed

- Dropped the unused `<Content Include="..\.dockerignore">` link and the leftover scaffold `hacman-app-back.http`
  file from the project.

### Added

- **Prometheus metrics** (`prometheus-net.AspNetCore`): a `GET /metrics` scrape endpoint (registered before
  authentication, so no JWT is required) exposing HTTP request count/duration/in-progress (`UseHttpMetrics`, labelled by
  method/route/status) alongside the default .NET runtime and process collectors. Adds one domain counter,
  `hacman_scores_submitted_total{difficulty}`, incremented post-commit in `LevelScoreService.CreateScore`; domain metric
  families are eagerly registered at startup so they appear in `/metrics` from boot. Documented in `README.md` →
  Observability.
- `POST /api/v1/level-scores` — record a new level-completion score for the authenticated user, atomically updating the
  user's cached best time for that gallery when the new score is a personal best (`SharedKernel.IUnitOfWork`-backed
  transaction).
- Integration tests for `LevelScoreRepository.CreateAsync` and `UserGalleryRepository.UpdateBestTimeIfBetterAsync`, and
  unit tests for the new `LevelScoreService.CreateScore` / `LevelScoreController.Create` flow.
- Pac-Man favicon on the Swagger UI (classic yellow, same shape as the frontend's), injected as an inline data URI via
  `SwaggerUIOptions.HeadContent` (Development only — Swagger is the API's only browser surface).
- `docs/ARCHITECTURE.md`, `docs/TESTING.md`, `docs/API.md`, `docs/CONTRIBUTING.md`, `docs/CHANGELOG.md`, and `LICENSE` (
  Apache 2.0).

## 2026-07-01 — 2026-07-02

### Changed

- Completed the **DDD physical relocation**: all five bounded contexts (Identity, PlayerProgress, Scoring,
  LevelTemplate, GalleryCatalog) moved from a technical-layer split (`controller/`, `service/`, `repository/`,
  `domain/`) into `Modules/<Context>/{Domain,Application,Infrastructure,Api}/`. `SharedKernel/IUnitOfWork` and
  `UnitOfWork` relocated out of the legacy `repository/` folder.
- Fixed `User`'s namespace (`hacman_app_back.config` → `hacman_app_back.Modules.Identity.Domain`).
- `UserGallery` unified as the single read/write model, replacing the separate `UserGalleryEntity` write model.
- `UserUpdater.CreateNewUserData` migrated to use `IUnitOfWork` instead of hand-rolled `NpgsqlTransaction` management.
- Corrected the unit-test filter documented in `CLAUDE.md`/CI usage from `Category!=Integration` (which silently matched
  everything) to the correct MSTest trait key, `TestCategory!=Integration`.

### Added

- `MazeLevel.Validate()` — invariant checks (cell grid vs. `Width`×`Height`, player-start bounds), logged as a warning (
  not thrown) when a stored row is malformed.
- `ILevelScoreRepository.GetTopTenScoresForGalleryAsync`, replacing the equivalent method previously on
  `IUserGalleryRepository`.
- Integration test coverage for previously untested repository methods across all contexts (
  `UserRepository.CreateAsync`, `LevelScoreRepository.GetScoresAsync`/`GetScoresByUserIdAsync`,
  `UserGalleryRepository.GetByGalleryIdAsync`, `UnitOfWork`'s real transactional path, `GalleryRepository`'s
  shared-connection overload, and an end-to-end test of `UserUpdater`'s cross-aggregate transaction).
- Rewrote several integration tests that previously wrapped assertions in `if (x != null)` (silently passing against
  missing seed data) to be self-contained: create their own fixture, assert unconditionally, clean up afterward.

### Fixed

- `MazeLevelRepository.MapMazeLevel` threw `InvalidCastException` when reading a row with a `NULL random_seed` — missing
  an `IsDBNull` check present on every other nullable column in the same method. Found and fixed while adding
  integration coverage for the malformed-row logging path.

## 2026-06-22 — 2026-06-27

### Added

- **LevelTemplate** bounded context: `MazeLevel` domain model and value objects (`Difficulty`, `EntityCategory`,
  `EntityDefinition`, `EntityScope`, `MazeDirection`, `MazeEntities`, `PlacementSurface`), `IMazeLevelRepository`/
  `MazeLevelRepository`, `IMazeLevelProvider`/`MazeLevelProvider`, `MazeLevelController`, `MazeLevelDto`.
- `GET /api/v1/maze/levels/{galleryId}` — retrieve maze level templates for a gallery.

### Changed

- Repository layer cleanup: consolidated logging, removed duplication, tightened interface completeness across all
  repositories.
- Controller layer cleanup: structured logging, unified JWT claims parsing via `AuthClaimsParser`, consistent Swagger
  XML docs.

## 2026-06-18 — 2026-06-20

### Added

- `ScoreEntryDto` introduced as the score API's response shape (replacing raw domain entities on the wire).
- Structured logging across `LevelScoreController`/`LevelScoreService` for score retrieval and error paths.

### Changed

- `LevelScore` domain model refactored to reference `Gallery`/`User` objects directly instead of flat ID fields, for
  clarity.
- `Gallery.GalleryId` changed to `int` with explicit JSON property naming across DTOs for stable frontend serialization.

## 2025-12-28 — 2025-12-29

### Added

- `UserGallery` domain model and `IUserGalleryProvider`, with repository-backed retrieval of a user's gallery unlock
  state.
- `UserGalleriesController` — `GET /api/v1/user-galleries`.
- Initial top-ten leaderboard query and scores UI groundwork.

## 2025-12-21 — 2025-12-23

### Added

- User management: `IUserRepository`/`UserRepository`, `IUserService`, JWT authentication wiring, `AuthClaimsParser`.
- `Gallery` and `UserGallery` repositories with integration tests.

### Fixed

- `UserUpdater` made transactional end-to-end, preventing a `User` row from being created without its corresponding
  `UserGallery` rows on partial failure — the original motivation for what later became `SharedKernel.IUnitOfWork`.

## 2025-12-13 — 2025-12-17

### Added

- Initial project scaffold: ASP.NET Core Web API, NHibernate integration, `User` entity.
- Gallery catalog foundation: `Gallery`, `LookupValue`, and `GalleryType`/`GallerySubtype`/`GalleryTheme` repositories,
  services, and controllers.
- CI configuration and Swagger setup.
