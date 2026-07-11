# hacman-app-front

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/) [![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4)](https://learn.microsoft.com/aspnet/core/blazor/) [![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](./LICENSE) [![Tests](https://img.shields.io/badge/tests-99%20passing-brightgreen)](./docs/TESTING.md)

Frontend user panel for **hacman-app** — a Blazor Web Server application for browsing photo galleries, tracking gallery
unlock/progress state, and viewing level-completion score rankings. Built with ASP.NET Core Blazor Server (.NET 10).

This is the frontend service. The companion ASP.NET Core Web API backend lives in [
`../hacman-app-back`](../hacman-app-back).

## Contents

- [Overview](#overview)
- [Tech stack](#tech-stack)
- [Getting started](#getting-started)
- [Configuration](#configuration)
- [Running](#running)
- [Pages & routes](#pages--routes)
- [Project structure](#project-structure)
- [Testing](#testing)
- [Further documentation](#further-documentation)
- [Versioning](#versioning)
- [Changelog](#changelog)
- [Author](#author)
- [License](#License)

## Overview

hacman-app-front is a server-rendered Blazor application that:

- authenticates users through **Nebula** — the HomeLab's authentication gateway, which proxies the **Andromeda**
  JWT-issuing service (a Java REST API; Nebula is the intermediary, not an OAuth2 provider) — storing the resulting JWT
  in an HTTP-only `accessToken` cookie,
- bootstraps the current user and their gallery state through a three-path flow (localStorage cache → server-side
  fetch → `app.js` JS-interop callback),
- browses the gallery catalog by type (Book / Character / Cosplay), theme, and subtype,
- shows per-gallery detail with an image lightbox, and
- renders per-difficulty score rankings for each gallery.

The user-bootstrap flow is the least-obvious part of the frontend; it is documented in [
`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Tech stack

| Concern      | Choice                                                                                            |
|--------------|---------------------------------------------------------------------------------------------------|
| Runtime      | .NET 10 / ASP.NET Core Blazor Server                                                              |
| Rendering    | Server-side interactive circuits (SignalR/WebSocket)                                              |
| Auth         | Nebula gateway → Andromeda (Java REST API, issues JWT) → JWT in an HTTP-only `accessToken` cookie |
| Backend API  | REST over HTTP(S), reached via `HacmanApiBaseUrl`                                                 |
| Client state | In-memory scoped session/gallery state + browser `localStorage` cache                             |
| Config       | `DotNetEnv` for `.env` loading                                                                    |
| Testing      | xUnit + bUnit + Moq (unit/component), Microsoft.Playwright (E2E)                                  |

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A reachable hacman-app-back instance (see [`../hacman-app-back`](../hacman-app-back)) for anything beyond the
  anonymous login screen
- (Optional) Docker + Docker Compose, if you prefer running the whole stack via `docker-compose up -d` from the
  repository root

### Clone & restore

```bash
git clone <repository-url>
cd hacman-app
dotnet restore ./hacman-app.sln
```

## Configuration

Configuration is loaded in this priority order: **User Secrets → Environment
Variables → `appsettings.{Environment}.json` → `appsettings.json`**.

Key configuration values:

| Variable                                                              | Purpose                                                                                                                             |
|-----------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------|
| `AppBasePath`                                                         | Base path prefix for routing/cookies (e.g. `/hacman/app`)                                                                           |
| `HacmanApiBaseUrl`                                                    | Public (browser-facing) base URL of the backend API                                                                                 |
| `HacmanApiInternalUrl`                                                | Optional server-to-server backend URL (e.g. inside Docker); falls back to `HacmanApiBaseUrl`                                        |
| `HacmanGameUrl`                                                       | URL to the Hacman game                                                                                                              |
| `HacmanResourcesUrl`                                                  | Base URL for gallery image resources                                                                                                |
| `NebulaApi:BaseUrl` / `NebulaApi:LoginUrl`                            | Nebula gateway base and login/redirect URLs                                                                                         |
| `NEBULA_CLIENT_ID` / `NEBULA_CLIENT_SECRET` / `NEBULA_TOKEN_ENDPOINT` | Nebula token-exchange credentials/endpoint (proxied to Andromeda)                                                                   |
| `TrustSelfSignedCertificates`                                         | Set to `true` to trust self-signed/private-CA upstream certificates outside Development (e.g. a Traefik-fronted homelab deployment) |

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the complete configuration reference, and
`hacman-app-front/appsettings.json` for defaults.

> **Deployment note:** if the frontend's upstreams (Nebula token endpoint, backend API) present self-signed or
> private-CA certificates, set `TrustSelfSignedCertificates=true` — otherwise server-side calls fail TLS validation with
`UntrustedRoot`.

## Running

```bash
cd hacman-app-front
dotnet run
```

```alert
Important: current authentication system uses httpOnly cookies for milkyway.test domain, so you must run the frontend on the same domain (or a subdomain) to avoid cross-origin cookie issues. For local development, you can use `localhost` or set up a local DNS entry for `milkyway.test`.
```

| Endpoint | URL                    | Working with auth URL (for local dev) |
|----------|------------------------|---------------------------------------|
| HTTP     | http://localhost:5196  | not working                           |
| HTTPS    | https://localhost:7205 | https://milkyway.test/dev/nebula/app/ |

Or start the full stack (backend + frontend) via Docker Compose from the repository root:

```bash
docker-compose up -d
```

## Pages & routes

| Route                             | Page                 | Description                                             |
|-----------------------------------|----------------------|---------------------------------------------------------|
| `/`                               | `Home`               | Landing/dashboard; anonymous users see the login prompt |
| `/book`, `/character`, `/cosplay` | `GalleryTypePage`    | Gallery listing for a type, with subtype filter buttons |
| `/theme`                          | `Theme`              | Galleries grouped by theme, then by type                |
| `/gallery/{id}`                   | `Gallery`            | Gallery detail with image lightbox and score rankings   |
| `/scores`, `/scores/{id}`         | `Scores`             | Per-difficulty score rankings                           |
| `/not-found`, `/Error`            | `NotFound` / `Error` | Fallback pages                                          |

## Project structure

```
hacman-app-front/
  Auth/                 Nebula callback + user-info controllers (cookie/JWT handling)
  Components/
    Layout/              MainLayout (thin), NavMenu
    Pages/                Home, Book/Character/Cosplay, Theme, Gallery, Scores, …
    Shared/                GalleryCard, GalleryTypePage (shared gallery-type skeleton)
  Domain/               Client-side models (Gallery, UserGallery, ScoreEntry, …)
  Services/             API clients, session/gallery state, bootstrap orchestration
  wwwroot/js/           app.js (browser-side bootstrap), localstorage.js
  Program.cs             Composition root & DI wiring
```

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the bootstrap flow, auth chain, and service responsibilities.

## Testing

```bash
# Unit + component tests (xUnit + bUnit)
dotnet test ./hacman-app-front.Tests/hacman-app-front.Tests.csproj

# End-to-end tests (Playwright) — deliberately excluded from hacman-app.sln
dotnet test ./hacman-app-front.E2E/hacman-app-front.E2E.csproj -c Release
```

99 unit/component tests currently pass. The E2E project (`hacman-app-front.E2E`) adds 4 Playwright smoke specs — an
anonymous bootstrap check (self-started published instance) plus three authenticated flows that run against the live
deployment. Full details, run modes, and Playwright setup are in [`docs/TESTING.md`](docs/TESTING.md).

## Further documentation

| Document                                       | Contents                                                                                            |
|------------------------------------------------|-----------------------------------------------------------------------------------------------------|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Three-path bootstrap flow, cookie/JWT auth chain, meta-tag → `app.js` config handoff, service layer |
| [`docs/TESTING.md`](docs/TESTING.md)           | Unit/bUnit/E2E categories, how to run each, Playwright browser setup                                |
| [`docs/CHANGELOG.md`](CHANGELOG.md)            | Notable frontend changes over time                                                                  |

## Versioning

The current frontend application version is tracked in `appsettings.json` under `App:Version` (currently `2.0.0`). This
follows [Semantic Versioning](https://semver.org/) informally; see [`CHANGELOG.md`](CHANGELOG.md) for what changed and
when.

## Changelog

See [`CHANGELOG.md`](docs/CHANGELOG.md).

## License

This project is licensed under the **Apache License 2.0**. See the [LICENSE](LICENSE) file for the full license text and
the [NOTICE](NOTICE) file for additional information.

---

## Author

**Szymon Derleta**  
GitHub: [@szymonderleta](https://github.com/szymonderleta)