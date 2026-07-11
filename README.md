# hacman-app

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/) [![Backend](https://img.shields.io/badge/backend-ASP.NET%20Core%20Web%20API-512BD4)](./hacman-app-back) [![Frontend](https://img.shields.io/badge/frontend-Blazor%20Server-512BD4)](./hacman-app-front) [![PostgreSQL](https://img.shields.io/badge/db-PostgreSQL-336791)](https://www.postgresql.org/) [![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](./hacman-app-back/LICENSE) [![Tests](https://img.shields.io/badge/tests-242%20unit%2Fintegration%20%2B%204%20E2E-brightgreen)](#testing)

**hacman-app** is a user panel for the **Hacman** game — it lets authenticated players browse photo galleries, unlock content, track per-level best times, and view score leaderboards. It is part of the **MilkyWay HomeLab** and authenticates through **Nebula**, the lab's authentication gateway, which proxies the **Andromeda** identity service (a Java REST API that issues JWT tokens).

The solution is a full-stack .NET 10 application: an ASP.NET Core Web API backend and a Blazor Server frontend, backed by PostgreSQL, with unit, integration, component, and end-to-end test suites.

## Contents

- [What's in this repository](#whats-in-this-repository)
- [Architecture at a glance](#architecture-at-a-glance)
- [Tech stack](#tech-stack)
- [Getting started](#getting-started)
- [Running](#running)
- [Testing](#testing)
- [CI/CD](#cicd)
- [Documentation](#documentation)
- [Versioning](#versioning)
- [Author](#author)
- [License](#license)

## What's in this repository

| Project | Type | In `hacman-app.sln`? | Description |
|---|---|---|---|
| [`hacman-app-back`](./hacman-app-back) | ASP.NET Core Web API | ✅ | REST API — users, gallery catalog, player progress, scoring, maze level templates. Organized by DDD bounded context. |
| [`hacman-app-front`](./hacman-app-front) | Blazor Web Server | ✅ | Server-rendered user panel — gallery browsing, detail lightbox, score rankings. Nebula-cookie auth. |
| [`hacman-app-back.Tests`](./hacman-app-back.Tests) | MSTest + xUnit + Moq | ✅ | Backend unit tests and Testcontainers-backed integration tests. |
| [`hacman-app-front.Tests`](./hacman-app-front.Tests) | xUnit + bUnit + Moq | ✅ | Frontend unit tests and bUnit component tests. |
| [`hacman-app-front.E2E`](./hacman-app-front.E2E) | Playwright + xUnit | ❌ (by design) | Browser end-to-end smoke tests; excluded from the solution because they need Playwright browsers + a running app. |

## Architecture at a glance

```
                    ┌─────────────────────────────────────────────┐
                    │                  Browser                     │
                    │  Blazor Server circuit (SignalR)  +  app.js  │
                    └───────────────┬─────────────────────────────┘
                                    │  HTTPS  (accessToken cookie)
                             ┌──────┴───────┐
                             │   Traefik    │  (reverse proxy, same-origin)
                             └──┬────────┬──┘
                   /hacman/app  │        │  /hacman/api
                        ┌───────┴──┐  ┌──┴─────────┐        ┌───────────────┐
                        │ frontend │  │  backend   │───────▶│  PostgreSQL   │
                        │ (Blazor) │  │ (Web API)  │  ADO   │  (Npgsql/SQL) │
                        └────┬─────┘  └──────┬─────┘        └───────────────┘
                             │               │
                             │ login redirect / token exchange, JWT validation
                             ▼               ▼
                        ┌────────────────────────────────┐
                        │  Nebula gateway  →  Andromeda   │
                        │  (Java REST API — issues JWT)   │
                        └────────────────────────────────┘
```

- **Auth:** users log in through **Nebula**, the HomeLab's authentication gateway, which proxies **Andromeda** — a Java REST API that issues the JWT tokens (Andromeda is the actual authorization system; Nebula is only the intermediary, not an OAuth2 provider). The frontend stores the resulting JWT in an HTTP-only `accessToken` cookie, and the backend validates it on each request. See each sub-project's architecture docs for the full chain.
- **Backend** is organized by **bounded context** (`Modules/Identity`, `PlayerProgress`, `Scoring`, `LevelTemplate`, `GalleryCatalog`), each with its own Domain/Application/Infrastructure/Api slices. Persistence is raw ADO.NET (`Npgsql`) with hand-written SQL.
- **Frontend** bootstraps the current user through a three-path flow (localStorage cache → server fetch → `app.js` JS-interop callback) — documented in [`hacman-app-front/docs/ARCHITECTURE.md`](./hacman-app-front/docs/ARCHITECTURE.md).

## Tech stack

| Concern | Backend | Frontend |
|---|---|---|
| Runtime | .NET 10 / ASP.NET Core Web API | .NET 10 / ASP.NET Core Blazor Server |
| Data | PostgreSQL via raw ADO.NET (`Npgsql`) | none (consumes the backend API) |
| Auth | JWT bearer (issued by Andromeda via Nebula) | Nebula gateway → JWT in HTTP-only `accessToken` cookie |
| API docs | Swagger / Swashbuckle | — |
| Testing | MSTest + xUnit + Moq + Testcontainers | xUnit + bUnit + Moq; Playwright (E2E) |
| Deployment | Docker image behind Traefik | Docker image behind Traefik |

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A reachable PostgreSQL instance (for the backend at runtime and for integration tests)
- (Optional) Docker + Docker Compose to run the whole stack
- (Optional, for E2E) Playwright browser binaries — see [`hacman-app-front/docs/TESTING.md`](./hacman-app-front/docs/TESTING.md)

### Clone, restore & build

```bash
git clone <repository-url>
cd hacman-app
dotnet restore ./hacman-app.sln
dotnet build ./hacman-app.sln -c Release --no-restore
```

Configuration for both apps is loaded in the order **User Secrets → Environment Variables → `appsettings.{Environment}.json` → (`.env`) → `appsettings.json`**. See each sub-project's README for its required variables.

## Running

### Locally (two terminals)

```bash
# Backend (needs a DB connection string)
cd hacman-app-back && dotnet run
#   HTTP http://localhost:8446 · HTTPS https://localhost:7037 · Swagger /swagger/index.html

# Frontend (separate terminal)
cd hacman-app-front && dotnet run
#   HTTP http://localhost:5196 · HTTPS https://localhost:7205
```

### Whole stack (Docker)

```bash
docker-compose up -d
```

## Testing

The three in-solution test projects run with a single command; the E2E project runs separately.

```bash
# Everything in the solution (backend + frontend unit/integration/component)
dotnet test ./hacman-app.sln

# Unit + component only, no database required
dotnet test ./hacman-app.sln --filter "TestCategory!=Integration"

# Frontend E2E (Playwright) — excluded from the solution, run explicitly
dotnet test ./hacman-app-front.E2E/hacman-app-front.E2E.csproj -c Release
```

| Suite | Count | Notes |
|---|---|---|
| Backend (`hacman-app-back.Tests`) | 143 (96 unit + 47 integration) | Integration tests use Testcontainers / a real PostgreSQL instance; marked `[TestCategory("Integration")]`. |
| Frontend unit + component (`hacman-app-front.Tests`) | 99 | xUnit + bUnit; no external dependencies. |
| Frontend E2E (`hacman-app-front.E2E`) | 4 | Playwright smoke specs; anonymous self-start + authenticated against a live deployment. |

Backend integration tests require a reachable PostgreSQL instance (via `TEST_HACMAN_CONNECTIONSTRING`). Full details in [`hacman-app-back/docs/TESTING.md`](./hacman-app-back/docs/TESTING.md) and [`hacman-app-front/docs/TESTING.md`](./hacman-app-front/docs/TESTING.md).

## CI/CD

GitHub Actions workflows in [`.github/workflows/`](./.github/workflows):

- `backend-cicd.yml` — on push to `dev`: restore → build → **test** → publish → Docker image → deploy.
- `frontend-cicd.yml` — same flow, without a test step (a conscious decision — the frontend test suites are run locally before merge).

CI triggers on push to `dev` only; it does not gate pull-request merges, so run `dotnet test` locally before merging.

## Documentation

| Document | Scope |
|---|---|
| [`hacman-app-back/README.md`](./hacman-app-back/README.md) | Backend overview, configuration, API |
| [`hacman-app-back/docs/ARCHITECTURE.md`](./hacman-app-back/docs/ARCHITECTURE.md) | Bounded contexts, layering, DB, auth, error handling |
| [`hacman-app-back/docs/API.md`](./hacman-app-back/docs/API.md) | Endpoint-by-endpoint reference |
| [`hacman-app-back/docs/TESTING.md`](./hacman-app-back/docs/TESTING.md) | Backend test guide |
| [`hacman-app-front/README.md`](./hacman-app-front/README.md) | Frontend overview, configuration, pages |
| [`hacman-app-front/docs/ARCHITECTURE.md`](./hacman-app-front/docs/ARCHITECTURE.md) | Three-path bootstrap, cookie/JWT auth, config handoff |
| [`hacman-app-front/docs/TESTING.md`](./hacman-app-front/docs/TESTING.md) | Frontend unit/bUnit/E2E guide |

## Versioning

Both apps are currently at **2.0.0**, tracked in each project's `appsettings.json` under `App:Version`. Notable changes are recorded in the per-project changelogs ([backend](./hacman-app-back/CHANGELOG.md), [frontend](./hacman-app-front/CHANGELOG.md)).

---

## License

This project is licensed under the **Apache License 2.0**. See the [LICENSE](LICENSE) file for the full license text and the [NOTICE](NOTICE) file for additional information.

---

## Author

**Szymon Derleta**  
GitHub: [@szymonderleta](https://github.com/szymonderleta)

## Project

Organization: [@MilkyWay-HomeLabs](https://github.com/MilkyWay-HomeLabs)  
Repository: [Hacman-Solution-Public](https://github.com/MilkyWay-HomeLabs/hacman-solution-public)
