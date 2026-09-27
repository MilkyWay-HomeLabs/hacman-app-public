# Changelog

All notable backend changes to `hacman-app-back` are documented here. Format
follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project does not yet follow
strict [Semantic Versioning](https://semver.org/) release tagging, so entries are grouped by date/theme rather than a
single authoritative version number. The current runtime version is tracked in `appsettings.json` → `App:Version`.

## [Unreleased]

## 2.3.0 — 2026-09-26

### Added
- **M7: gameplay-telemetry achievements tier.** Three new categories on top of the 100-achievement
  v1 catalogue — `COMBAT` (enemies defeated), `POWER_UPS` (buffs collected), `SURVIVAL` (hits
  taken, framed as no-hit/survival counts) — 10 achievements each, **130 total**. Backed by three
  new `level_scores` columns (`enemies_defeated`, `buffs_collected`, `hits_taken`) and three new
  `achievement_category_enum` values, applied to both `test_hacman` and prod `hacman`.
- `POST /v1/level-scores` now persists the three telemetry counters (optional, default 0 —
  backward compatible with every existing caller) and echoes any achievements the submission just
  leveled up via `ScoreEntryDto.newlyUnlockedAchievements`.
- Evaluation-engine calculators for all 30 new metrics, following the same
  `AchievementMetricSqlRegistry` pattern as v1's 100 (see `repo/docs/hacman/DECISIONS.md` D-011).

### Fixed
- `AchievementCategory` enum was missing the three new telemetry categories, causing
  `AchievementDefRepository.MapAchievementDef`'s `Enum.Parse` to throw on every one of the 30 new
  catalogue rows — this silently blocked every deploy of the M7 work until fixed. See
  `repo/docs/hacman/infra/HANDOFF.md` §5a for the full incident writeup.

### Changed
- Bumped `App:Version` / `InformationalVersion` to `2.3.0` (`appsettings.json`,
  `appsettings.Development.json`).

## 2.2.0 — 2026-09-25

### Added
- **Achievements feature**: a new `Modules/Achievements/` bounded context (`Domain → Application → Infrastructure →
  Api`, matching the five pre-existing modules), backed by two new tables (`achievement_def`, `achievement_progress`)
  applied to both `test_hacman` and prod `hacman`. Catalogue is **100 achievements across 10 categories**
  (Collection, Mastery Easy/Medium/Hard, Completion, Volume, Variety, Speed, Ranking, Longevity) — every content-
  coverage metric expressed as a live `*_pct` percentage of that environment's own data, never a hardcoded count,
  so the same catalogue is correct in both test and prod despite their different content volumes. Progress level is
  always derived at read time from the raw counter and `achievement_def.thresholds` — no stored `level`/`stars`
  column to drift out of sync after a threshold re-tune.
- `GET /api/v1/achievements/catalog` and `GET /api/v1/achievements/me` (per-user progress, server-side joined
  against the catalogue).
- Achievement evaluation wired into `LevelScoreService.CreateScore`, inside the existing `IUnitOfWork` transaction,
  after the score/best-time writes commit.
- **Nebula achievements-ingestion adapter** (`NebulaAchievementClient` + `NebulaAchievementDispatcher`): pushes the
  full catalogue on every boot (idempotent upsert) and per-user progress after every score-submission commit, on a
  dedicated background `Channel`-backed `BackgroundService` that swallows every exception — a Nebula outage never
  affects a score submission. Ships dark (no-op) until `NEBULA_ACHIEVEMENTS_URL`/`NEBULA_ACHIEVEMENTS_TOKEN` are
  configured with real values.
- A `dotnet-coverage` CI check step enforcing 80% line coverage, scoped to `Modules/Achievements` only.
- Integration tests for every metric calculator against a real PostgreSQL Testcontainer, and adapter tests against
  a fake HTTP handler (never the real `nebula-rest-api`).

Full design rationale for the achievements feature (category choices, schema shape, evaluation trigger point,
Nebula adapter pattern, coverage-gate scope) is recorded in `repo/docs/hacman/DECISIONS.md`.

### Changed
- Bumped `App:Version` / `InformationalVersion` to `2.2.0` (`appsettings.json`, `appsettings.Development.json`).

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
