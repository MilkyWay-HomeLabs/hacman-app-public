# Architecture

This document describes the current, live architecture of `hacman-app-back`. It is kept in sync with the codebase — if
something here looks stale, the code is the source of truth (see [`CONTRIBUTING.md`](../CONTRIBUTING.md) for the
doc-maintenance rule).

## Contents

- [High-level shape](#high-level-shape)
- [Bounded contexts](#bounded-contexts)
- [Layering within a context](#layering-within-a-context)
- [DTO conventions](#dto-conventions)
- [Error handling](#error-handling)
- [Authentication flow](#authentication chain-(cookiejwt))
- [Database](#database)
- [Key configuration](#key-configuration)
- [Composition root](#composition-root)
- [Design history](#design-history)

## High-level shape

The backend is organized by **bounded context**, not by technical layer. Instead of one big `controllers/`, `services/`,
`repositories/` split across the whole app, each business area owns its own vertical slice:

```
hacman-app-back/
  Modules/
    Identity/          Domain, Application, Infrastructure, Api
    PlayerProgress/     Domain, Application, Infrastructure, Api
    Scoring/             Domain, Application, Infrastructure, Api
    LevelTemplate/        Domain, Application, Infrastructure, Api
    GalleryCatalog/        Domain, Application, Infrastructure, Api
  SharedKernel/         IUnitOfWork / UnitOfWork — cross-context transaction abstraction
  config/                NHibernateHelper.cs (legacy, see Database section)
  Program.cs             Composition root
```

This is the result of a deliberate, incremental migration from a technical-layer split (`controller/` → `service/` →
`repository/` → `domain/`) to context-first modules. Cross-context references are expected and normal — the migration
relocated files, it did not change relationships. For example, `Modules/Scoring/Domain/LevelScore.cs` references both
`Modules/Identity/Domain/User.cs` and `Modules/GalleryCatalog/Domain/Gallery.cs`, because a score belongs to a user and
a gallery.

## Bounded contexts

| Context            | Aggregate root             | Owns                                                                                                                                                                                                                                                                                                                                    |
|--------------------|----------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Identity**       | `User`                     | `User`, `IUserRepository`/`UserRepository`, `IUserProvider`/`UserProvider`, `IUserUpdater`/`UserUpdater`, `UsersController`, `CreateUserRequest`, `AuthClaimsParser` (parses the JWT `sub` claim, format `"authId,email"`)                                                                                                              |
| **PlayerProgress** | `UserGallery`              | Per-user gallery unlock state and cached best time. `UserGallery`, `IUserGalleryRepository`/`UserGalleryRepository`, `IUserGalleryProvider`/`UserGalleryProvider`, `UserGalleriesController`                                                                                                                                            |
| **Scoring**        | `LevelScore` (append-only) | Level-completion score history and leaderboard queries. `LevelScore`, `ILevelScoreRepository`/`LevelScoreRepository`, `ILevelScoreService`/`LevelScoreService`, `LevelScoreController`, `ScoreEntryDto`                                                                                                                                 |
| **LevelTemplate**  | `MazeLevel`                | Maze level definitions consumed by the game client. `MazeLevel` + value objects (`Difficulty`, `PlacementSurface`, `MazeDirection`, `EntityCategory`, `EntityScope`, `MazeEntities`, `EntityDefinition`), `IMazeLevelRepository`/`MazeLevelRepository`, `IMazeLevelProvider`/`MazeLevelProvider`, `MazeLevelController`, `MazeLevelDto` |
| **GalleryCatalog** | `Gallery`                  | Gallery metadata and lookup values. `Gallery`, `LookupValue`, `GalleryType`/`GallerySubtype`/`GalleryTheme` repositories, services, and controllers                                                                                                                                                                                     |

Cross-cutting infrastructure that doesn't belong to any single context stays outside `Modules/`:

- `SharedKernel/IUnitOfWork.cs` / `UnitOfWork.cs` — shared transaction abstraction wrapping a single
  `NpgsqlConnection` + `NpgsqlTransaction`, used for cross-aggregate operations (e.g. creating a `User` plus its
  `UserGallery` rows atomically, or recording a `LevelScore` and updating the cached best time in the same transaction).
- `config/NHibernateHelper.cs` — NHibernate session factory setup. Largely unused in practice (
  see [Database](#database)).
- `Program.cs` stays at the project root by ASP.NET Core convention — it isn't part of any context, it's the composition
  root that wires all of them together.

## Layering within a context

Every module follows the same four-layer shape:

| Layer             | Responsibility                                                                                                    | Naming                                                                       |
|-------------------|-------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------|
| `Domain/`         | Entities and value objects. Plain C# classes; some (e.g. `MazeLevel`) carry invariants via a `Validate()` method. | —                                                                            |
| `Infrastructure/` | Data access. Raw ADO.NET (`Npgsql`) repositories with hand-written SQL.                                           | Interface `I*Repository`, implementation `*Repository`                       |
| `Application/`    | Business logic / orchestration.                                                                                   | Interface `I*Service` or `I*Provider`, implementation `*Service`/`*Provider` |
| `Api/`            | HTTP surface: controllers and DTOs.                                                                               | `*Controller`, `*Dto` (payloads), `*Request` (action-specific input)         |

Controllers generally return DTOs rather than domain entities (e.g. `MazeLevelController` returns `MazeLevelDto`).
`UsersController` is the current exception and returns the `User` domain entity directly. Mapping from entity to DTO is
done by hand via a private static `MapToDto` method in the service layer (see
`Modules/Scoring/Application/LevelScoreService.cs`) — there is no AutoMapper or similar mapping library.

## DTO conventions

- Every context keeps its DTOs in its own `Modules/<Context>/Api/` folder.
- `*Dto` for API response payloads, `*Request` for action-specific input models (e.g. `CreateUserRequest`,
  `CreateLevelScoreRequest`).

## Error handling

There is no global exception-handling middleware or `ProblemDetails` configuration. Each controller action wraps its
logic in `try/catch` and returns errors via the standard ASP.NET Core `ControllerBase` helpers:

| Status                      | When                                                                                    | Shape                                                        |
|-----------------------------|-----------------------------------------------------------------------------------------|--------------------------------------------------------------|
| `400 Bad Request`           | Invalid input (missing/empty required field)                                            | `BadRequest(new { message = "..." })` or `BadRequest("...")` |
| `401 Unauthorized`          | Invalid/missing JWT claims, or the user isn't mapped to the identity                    | `Unauthorized(new { message = "..." })`                      |
| `404 Not Found`             | Entity not found                                                                        | `NotFound()`                                                 |
| `500 Internal Server Error` | Unhandled exception, caught by a top-level `catch (Exception ex)`, logged via `ILogger` | `StatusCode(500, "An unexpected error occurred.")`           |

`403 Forbidden` is not currently used anywhere. New endpoints should declare their possible responses with
`[ProducesResponseType(StatusCodes.Status...)]` attributes.

## Authentication chain (cookie/JWT)

Authentication is delegated to **Nebula**, the HomeLab's authentication gateway. Nebula is **not** an OAuth2 provider —
it is an intermediary that proxies **Andromeda**, a Java REST API that is the actual authorization system and issues the
JWT tokens. The frontend talks only to Nebula; users never enter credentials into Hacman.

### User flow

1. **Initial load**: Frontend checks if the `accessToken` HTTP-only cookie exists and is valid (unexpired).
2. **If missing or expired**: User is redirected to `NebulaApi:LoginUrl` (Nebula's login page).
3. **User logs in to Nebula**: The user enters credentials **in Nebula**, not in Hacman. Nebula validates them against *
   *Andromeda** (the actual authorization system).
4. **Andromeda issues a token**: If credentials are valid, Andromeda returns a token to Nebula.
5. **Nebula redirects back**: Nebula redirects the browser to `GET /auth/callback?code=…&returnUrl=…` ([
   `Auth/AuthController.cs`](../Auth/AuthController.cs)) with an authorization code.
6. **Backend exchanges code for tokens**: `Callback` exchanges the `code` at the Nebula token endpoint (
   `RequestNebulaTokenAsync`, shared with `Refresh`) for an `access_token` (JWT) and `refresh_token`.
7. **Tokens stored as HTTP-only cookies**: Both tokens are stored as **HTTP-only cookies** (`accessToken`,
   `refreshToken`) via `BuildCookieOptions`: `HttpOnly=true`, `Secure` outside Development, `SameSite=Lax`, `Path=/`.
   Page JavaScript cannot read them.
8. **Subsequent requests carry the token**: Every request — browser→frontend (`app.js`, `credentials: 'include'`) and
   frontend server→backend (the API client's cookie container) — carries the `accessToken` cookie. The backend validates
   the JWT (note: it validates lifetime only; see the backend architecture docs).
9. **Token refresh on expiry**: On a `401 Unauthorized` response, `app.js` calls `POST /auth/refresh`, which uses the
   `refreshToken` cookie to obtain a rotated pair, then retries — up to `MaxRedirectAttempts` before redirecting back to
   Nebula.

## Database

- **PostgreSQL**, schema configurable via `NHibernate__Schema` (default: `hacman`; test schema: `test_hacman`).
- **NHibernate 5.6 is wired up but not actually used for data access.** Only `UserMap` (
  `Modules/Identity/Infrastructure/UserMap.cs`) is a mapping-by-code `ClassMapping<User>`, registered in
  `config/NHibernateHelper.cs`. In practice, every repository class (`Modules/<Context>/Infrastructure/*Repository.cs`)
  reads/writes through raw ADO.NET (`Npgsql`) with hand-written SQL — the `ISessionFactory`/`ISession` DI registration
  in `Program.cs` exists but has no consumers. Removing this dead dependency is a tracked backlog item (see design
  history).
- Repository-layer integration tests run against a real PostgreSQL instance (a long-lived `milky-test-postgres` Docker
  container in local dev, or Testcontainers-provisioned in CI) — see [`TESTING.md`](TESTING.md).
- Cross-aggregate writes (e.g. `User` + N `UserGallery` rows, or a `LevelScore` insert + `UserGallery.best_time_ms`
  update) share a single connection/transaction via `SharedKernel.IUnitOfWork`, so a partial failure rolls back
  everything.

## Key configuration

Loaded in priority order: **User Secrets → Environment Variables → `appsettings.{Environment}.json` → `.env`
file → `appsettings.json`**.

| Variable                                     | Purpose                                                            |
|----------------------------------------------|--------------------------------------------------------------------|
| `ConnectionStrings__Default`                 | PostgreSQL connection string for runtime                           |
| `TEST_HACMAN_CONNECTIONSTRING`               | PostgreSQL connection string for integration tests                 |
| `NHibernate__Schema`                         | DB schema name                                                     |
| `Jwt__Key` / `Jwt__Issuer` / `Jwt__Audience` | JWT bearer token validation                                        |
| `App__Version`                               | Displayed application version (`appsettings.json` → `App:Version`) |

## Composition root

`Program.cs` wires up all module dependencies: it registers each `I*Repository`/`I*Service`/`I*Provider` pair per
context, the JWT bearer authentication scheme, Swagger generation, CORS, HTTP logging middleware, and the
`Func<IUnitOfWork>` factory shared by services that need cross-aggregate transactions.

## Design history

The move from a technical-layer split to bounded-context modules, and the ongoing integration-test-coverage effort, were
carried out as a series of small, independently-verified iterations.