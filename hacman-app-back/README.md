# hacman-app-back

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/) [![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](./LICENSE) [![Tests](https://img.shields.io/badge/tests-143%20passing-brightgreen)](./docs/TESTING.md)

Backend API for **hacman-app** — a user panel for managing photo galleries, unlocking content, and tracking level completion scores. Built with ASP.NET Core Web API (.NET 10), PostgreSQL, and a Domain-Driven Design–inspired module layout.

This is the backend service. The companion Blazor Web Server frontend lives in [`../hacman-app-front`](../hacman-app-front).

## Contents

- [Overview](#overview)
- [Tech stack](#tech-stack)
- [Getting started](#getting-started)
- [Configuration](#configuration)
- [Running](#running)
- [API overview](#api-overview)
- [Observability](#observability)
- [Project structure](#project-structure)
- [Testing](#testing)
- [Further documentation](#further-documentation)
- [Versioning](#versioning)
- [Changelog](#changelog)
- [Author](#author)
- [License](#license)

## Overview

hacman-app-back exposes a REST API that:

- authenticates users via JWT bearer tokens issued through Nebula, the HomeLab's authentication gateway that proxies the Andromeda identity service (a Java REST API; Nebula is not an OAuth2 provider),
- manages user accounts and their gallery unlock/progress state,
- serves gallery catalog metadata (types, themes, subtypes),
- serves maze level templates for the Hacman game,
- records level-completion scores and per-user/per-gallery best times, and
- exposes a leaderboard/score-listing API.

The codebase is organized by **bounded context** (Identity, PlayerProgress, Scoring, LevelTemplate, GalleryCatalog) rather than by technical layer. See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the full breakdown.

## Tech stack

| Concern  | Choice                                                                                                |
|----------|-------------------------------------------------------------------------------------------------------|
| Runtime  | .NET 10 / ASP.NET Core Web API                                                                        |
| Database | PostgreSQL (raw ADO.NET via `Npgsql`, hand-written SQL)                                               |
| ORM      | NHibernate 5.6 is wired up but effectively unused (see [Architecture](docs/ARCHITECTURE.md#database)) |
| Auth     | JWT bearer tokens, issued by the Andromeda identity service via the Nebula gateway                    |
| API docs | Swashbuckle / Swagger UI                                                                              |
| Testing  | MSTest + xUnit + Moq, integration tests against a real PostgreSQL instance                            |

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A reachable PostgreSQL instance (local, Docker, or the shared test container — see [Testing](#testing))
- (Optional) Docker + Docker Compose, if you prefer running the whole stack via `docker-compose up -d` from the repository root

### Clone & restore

```bash
git clone <repository-url>
cd hacman-app
dotnet restore ./hacman-app.sln
```

## Configuration

Configuration is loaded in this priority order: **User Secrets → Environment Variables → `appsettings.{Environment}.json` → `.env` file → `appsettings.json`**.

Minimum required environment variables to run the backend:

| Variable                                     | Purpose                                              |
|----------------------------------------------|------------------------------------------------------|
| `ConnectionStrings__Default`                 | PostgreSQL connection string used at runtime         |
| `NHibernate__Schema`                         | DB schema name (default: `hacman`)                   |
| `Jwt__Key` / `Jwt__Issuer` / `Jwt__Audience` | JWT validation parameters for incoming bearer tokens |

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md#key-configuration) for the complete configuration reference, and `hacman-app-back/appsettings.json` for defaults.

## Running

```bash
cd hacman-app-back
dotnet run
```

```alert
Important: current authentication system uses httpOnly cookies for milkyway.test domain, so you must run the frontend on the same domain (or a subdomain) to avoid cross-origin cookie issues. For local development, you can use `localhost` or set up a local DNS entry for `milkyway.test`.
```

| Endpoint   | URL                                       | Working with auth URL (for local dev)                   |
|------------|-------------------------------------------|---------------------------------------------------------|
| HTTP       | http://localhost:8446                     | not working                                             |
| HTTPS      | https://localhost:7037                    | https://milkyway.test/dev/nebula/api/                   |
| Swagger UI | https://localhost:7037/swagger/index.html | https://milkyway.test/dev/nebula/api/swagger/index.html |

Or start the full stack (backend + frontend) via Docker Compose from the repository root:

```bash
docker-compose up -d
```

## API overview

All endpoints are versioned under `/api/v1/`. Full request/response details are documented in Swagger UI at runtime and in [`docs/API.md`](docs/API.md).

| Method | Route                                | Auth | Description                                          |
|--------|--------------------------------------|------|------------------------------------------------------|
| `GET`  | `/api/v1/users/{id}`                 | –    | Get a user by internal ID                            |
| `GET`  | `/api/v1/users/external-auth-id`     | JWT  | Get the authenticated user                           |
| `POST` | `/api/v1/users`                      | JWT  | Create a user account for the authenticated identity |
| `GET`  | `/api/v1/user-galleries`             | JWT  | Get gallery unlock/progress state for a user         |
| `GET`  | `/api/v1/level-scores`               | –    | Paginated score listing for a gallery                |
| `GET`  | `/api/v1/level-scores/user/{userId}` | –    | Scores for a specific user                           |
| `POST` | `/api/v1/level-scores`               | JWT  | Record a new score, update cached best time          |
| `GET`  | `/api/v1/maze/levels/{galleryId}`    | –    | Maze level templates for a gallery                   |
| `GET`  | `/api/v1/galleries/types`            | –    | All gallery types                                    |
| `GET`  | `/api/v1/galleries/themes`           | –    | All gallery themes                                   |
| `GET`  | `/api/v1/galleries/subtypes`         | –    | All gallery subtypes                                 |

## Observability

### Structured logging

Every non-trivial operation logs entry/exit, errors, and external calls through `ILogger<T>`. HTTP request logging is enabled via `UseHttpLogging` (header values are redacted by default).

### Prometheus metrics

The API exposes a Prometheus-compatible scrape endpoint (via [`prometheus-net.AspNetCore`](https://github.com/prometheus-net/prometheus-net)):

```
GET /metrics
```

The endpoint is registered **before authentication**, so a scraper reaches it without a JWT (Prometheus scrapes the container directly on its port inside the Docker network). The `/metrics` scrape itself is terminal and is not counted as an HTTP request metric.

**Exposed metrics:**

| Metric                          | Type      | Labels                                   | Source           | Description                                                                                      |
|---------------------------------|-----------|------------------------------------------|------------------|--------------------------------------------------------------------------------------------------|
| `http_requests_received_total`  | counter   | `method`, `code`, `controller`, `action` | `UseHttpMetrics` | Total HTTP requests processed                                                                    |
| `http_request_duration_seconds` | histogram | `method`, `code`, `controller`, `action` | `UseHttpMetrics` | Request duration                                                                                 |
| `http_requests_in_progress`     | gauge     | `method`, `controller`, `action`         | `UseHttpMetrics` | In-flight requests                                                                               |
| `hacman_scores_submitted_total` | counter   | `difficulty`                             | domain           | Level scores successfully submitted (incremented post-commit in `LevelScoreService.CreateScore`) |
| `dotnet_*`, `process_*`         | various   | –                                        | runtime          | GC, memory, CPU, thread, and process metrics                                                     |

Domain metric families are eagerly registered at startup (see `Program.cs`), so `hacman_scores_submitted_total` appears in `/metrics` from boot with its `# HELP`/`# TYPE` lines; per-`difficulty` series are created on the first submission of each difficulty. New domain metrics should follow the same pattern: a static `Counter`/`Gauge`/`Histogram` on the owning service, incremented at the meaningful point, and eagerly touched in the composition root.

## Project structure

```
hacman-app-back/
  Modules/
    Identity/          User accounts, JWT claims parsing
    PlayerProgress/     Per-user gallery unlock state & best times
    Scoring/             Level-completion score history & leaderboards
    LevelTemplate/        Maze level definitions for the game
    GalleryCatalog/        Gallery metadata (type/theme/subtype)
  SharedKernel/         Cross-context infrastructure (IUnitOfWork)
  config/                Legacy NHibernate session-factory setup
  Program.cs             Composition root
```

Each module keeps its own `Domain/`, `Application/`, `Infrastructure/`, and `Api/` slice. See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the rationale and layer responsibilities.

## Testing

```bash
dotnet test ./hacman-app-back.Tests/hacman-app-back.Tests.csproj
```

143 tests (96 unit + 47 integration) currently pass. Integration tests require a reachable PostgreSQL instance via `TEST_HACMAN_CONNECTIONSTRING`. Full details, conventions, and how to run subsets are in [`docs/TESTING.md`](docs/TESTING.md).

## Further documentation

| Document                                       | Contents                                                                    |
|------------------------------------------------|-----------------------------------------------------------------------------|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Bounded contexts, layering, database, auth flow, error-handling conventions |
| [`docs/TESTING.md`](docs/TESTING.md)           | Test categories, how to run them, fixture conventions, coverage by module   |
| [`docs/API.md`](docs/API.md)                   | Endpoint-by-endpoint request/response reference                             |
| [`CHANGELOG.md`](CHANGELOG.md)                 | Notable backend changes over time                                           |

## Versioning

The current backend application version is tracked in `appsettings.json` under `App:Version` (currently `2.1.0`). This follows [Semantic Versioning](https://semver.org/) informally; see [`CHANGELOG.md`](CHANGELOG.md) for what changed and when.

## Changelog

See [`CHANGELOG.md`](docs/CHANGELOG.md).

## License

This project is licensed under the **Apache License 2.0**. See the [LICENSE](LICENSE) file for the full license text and the [NOTICE](NOTICE) file for additional information.

---

## Author

**Szymon Derleta**  
GitHub: [@szymonderleta](https://github.com/szymonderleta)