# Architecture

Architecture reference for **hacman-app-front**, the Blazor Web Server frontend of hacman-app. For a high-level overview and how to run the app, see [`../README.md`](../README.md).

## Contents

- [High-level shape](#high-level-shape)
- [The user-bootstrap flow](#the-user-bootstrap-flow)
- [Authentication chain (cookie/JWT)](#authentication-chain-cookiejwt)
- [Configuration handoff (meta tags → app.js)](#configuration-handoff-meta-tags--appjs)
- [Service layer](#service-layer)
- [Component layer](#component-layer)
- [Key configuration](#key-configuration)
- [Security notes](#security-notes)

## High-level shape

The frontend is an ASP.NET Core **Blazor Server** application: pages render server-side and run over an interactive SignalR/WebSocket circuit. It has no database of its own — all data comes from the backend API (`hacman-app-back`), reached over HTTP(S).

Two independent runtimes cooperate during startup:

- the **Blazor server circuit** (C#), which renders components and holds scoped per-user state, and
- the **browser** (`wwwroot/js/app.js`), which runs an immediately-invoked bootstrap that talks to the backend directly and pushes results back into Blazor via JS interop.

The reason for this split is deployment topology: in production the browser reaches the backend same-origin through Traefik, but the Blazor *server* process cannot always reach the backend directly (or only slowly) from inside its container. So the browser does the authoritative bootstrap, and the server side is a cache-first fast path plus a fallback.

## The user-bootstrap flow

Resolving "who is the current user, and what are their galleries" happens through **three paths**, orchestrated by [`Services/UserBootstrapService.cs`](../Services/UserBootstrapService.cs) and driven from [`Components/Layout/MainLayout.razor`](../Components/Layout/MainLayout.razor). Whichever path resolves the user first wins; the others become no-ops.

```
                         page load
                             │
        ┌────────────────────┴─────────────────────┐
        │                                           │
  Blazor circuit                              browser (app.js IIFE)
  MainLayout.OnAfterRenderAsync              runs immediately on load
        │                                           │
        │ 1. registers JS callback listener         │ GET  auth/user/bootstrap  (validates cookie)
        │    (hacman_registerBootstrapListener)     │   └ 401 → POST auth/refresh → retry / redirect to Nebula
        │                                           │ GET  {api}/v1/users/external-auth-id
        │ 2. UserBootstrapService.LoadUserDataAsync │   └ 404 → POST {api}/v1/users  (create)
        │      ├─ (A) localStorage cache            │ GET  {api}/v1/user-galleries?userId=…
        │      │      hacmanUser + hacmanUserGalleries
        │      └─ (B) server fetch (fast-fail)      │ writes hacmanUser + hacmanUserGalleries → localStorage
        │            GET external-auth-id           │ dispatches 'hacman:user-bootstrap-ready'
        │                                           │
        └───────────────► (C) JS callback ◄─────────┘
              MainLayout.ReceiveBootstrapData(json)
              → UserBootstrapService.ApplyBootstrapPayloadAsync
```

**Path A — localStorage cache** (fast path, `LoadUserDataAsync`): reads `hacmanUser` and `hacmanUserGalleries` written by a prior session or a prior app.js run. If both are present, the user is resolved with zero network calls. This is the common warm-start case.

**Path B — server fetch** (fallback within `LoadUserDataAsync`): if the cache is empty, the server makes a single `GET /v1/users/external-auth-id` call through the backend API client. The `HttpClient` has a short timeout, so if the backend is unreachable from the server process it fails fast and defers to path C rather than hanging.

**Path C — JS callback** (authoritative, `ApplyBootstrapPayloadAsync`): `app.js` performs the full browser-side bootstrap (validate cookie → resolve/create Hacman user → fetch galleries → persist to localStorage), then dispatches `hacman:user-bootstrap-ready`. `MainLayout` registered a `DotNetObjectReference` listener for this event, so the browser calls back into `[JSInvokable] ReceiveBootstrapData`, which applies the payload. If a user is already resolved (A or B won), this is skipped.

### Late-registration replay

`app.js` runs immediately on page load, but the Blazor circuit's `OnAfterRenderAsync` (where the listener registers) happens later. To avoid a lost event, `app.js` **caches** the last bootstrap payload and any auth error; when Blazor registers its listener, `hacman_registerBootstrapListener` replays the cached payload synchronously.

The replay deliberately **omits `hacmanUserGalleries`** (sends an empty array): 320+ gallery objects can exceed Blazor's default 32 KB SignalR message limit and tear down the circuit in a reconnect loop. `ReceiveBootstrapData` → `ApplyBootstrapPayloadAsync` therefore reads galleries from **localStorage** (path A's `TryLoadGalleriesFromCacheAsync`) rather than from the interop payload.

### Failure handling

On a critical browser-side error, `app.js` falls back to cached `userData` + `hacmanUser` from localStorage if present (offline mode); otherwise it dispatches `hacman:auth-error`, which reaches `MainLayout.ReceiveAuthError` and surfaces the login/error banner. Missing tokens or a redirect loop (bounded by `MaxRedirectAttempts`) send the user back to Nebula login.

## Authentication chain (cookie/JWT)

Authentication is delegated to **Nebula**, the HomeLab's authentication gateway. Nebula is **not** an OAuth2 provider — it is an intermediary that proxies **Andromeda**, a Java REST API that is the actual authorization system and issues the JWT tokens. The frontend talks only to Nebula; users never enter credentials into Hacman.

### User flow

1. **Initial load**: Frontend checks if the `accessToken` HTTP-only cookie exists and is valid (unexpired).
2. **If missing or expired**: User is redirected to `NebulaApi:LoginUrl` (Nebula's login page).
3. **User logs in to Nebula**: The user enters credentials **in Nebula**, not in Hacman. Nebula validates them against **Andromeda** (the actual authorization system).
4. **Andromeda issues a token**: If credentials are valid, Andromeda returns a token to Nebula.
5. **Nebula redirects back**: Nebula redirects the browser to `GET /auth/callback?code=…&returnUrl=…` ([`Auth/AuthController.cs`](../Auth/AuthController.cs)) with an authorization code.
6. **Backend exchanges code for tokens**: `Callback` exchanges the `code` at the Nebula token endpoint (`RequestNebulaTokenAsync`, shared with `Refresh`) for an `access_token` (JWT) and `refresh_token`.
7. **Tokens stored as HTTP-only cookies**: Both tokens are stored as **HTTP-only cookies** (`accessToken`, `refreshToken`) via `BuildCookieOptions`: `HttpOnly=true`, `Secure` outside Development, `SameSite=Lax`, `Path=/`. Page JavaScript cannot read them.
8. **Subsequent requests carry the token**: Every request — browser→frontend (`app.js`, `credentials: 'include'`) and frontend server→backend (the API client's cookie container) — carries the `accessToken` cookie. The backend validates the JWT (note: it validates lifetime only; see the backend architecture docs).
9. **Token refresh on expiry**: On a `401 Unauthorized` response, `app.js` calls `POST /auth/refresh`, which uses the `refreshToken` cookie to obtain a rotated pair, then retries — up to `MaxRedirectAttempts` before redirecting back to Nebula.

### Token validation on the frontend

The frontend's own `GET /auth/user/bootstrap` and `GET /auth/user/me` ([`Auth/UserInfoController.cs`](../Auth/UserInfoController.cs)) validate the `accessToken` cookie and return the decoded Nebula claims (`sub`/`email`/`preferred_username`/…). They **base64url-decode the JWT payload only** — they do not verify the signature (that is the backend's job); their purpose is to gate the bootstrap and surface the Nebula identity. Hacman user data and galleries are fetched client-side by `app.js` directly from the backend API.

## Configuration handoff (meta tags → app.js)

`app.js` runs in the browser and cannot read server `IConfiguration`. The bridge is a set of **meta tags** rendered by [`Components/App.razor`](../Components/App.razor) into the page `<head>`:

| Meta tag                             | Source config key     | Read by                                            |
|--------------------------------------|-----------------------|----------------------------------------------------|
| `<base href>`                        | `AppBasePath`         | `app.js` `appBaseHref()` (relative `auth/*` calls) |
| `meta[name="hacman-api-base-url"]`   | `HacmanApiBaseUrl`    | `app.js` `getHacmanApiBaseUrl()`                   |
| `meta[name="nebula-login-url"]`      | `NebulaApi:LoginUrl`  | `app.js` `getNebulaLoginUrl()`                     |
| `meta[name="max-redirect-attempts"]` | `MaxRedirectAttempts` | `app.js` `getMaxRedirectAttempts()`                |

Each getter falls back to a milkyway.test default if the tag is missing. `App.razor` also loads the two scripts in order: `blazor.web.js`, then `localstorage.js` (the interop wrapper behind `ILocalStorageService`), then `app.js`.

## Service layer

Registered in [`Program.cs`](../Program.cs); lifetimes matter in Blazor Server (a singleton holding per-user data leaks across circuits — see [Security notes](#security-notes)).

| Service                                                            | Lifetime        | Responsibility                                                                               |
|--------------------------------------------------------------------|-----------------|----------------------------------------------------------------------------------------------|
| `UserSessionState`                                                 | Scoped          | Holds the JWT/access token and the current `UserResponse` for the circuit                    |
| `UserGalleriesState`                                               | Scoped          | Cached, sorted gallery list + `UnlockedCount`, with an `OnChange` event; paged via `GetPage` |
| `UserBootstrapService`                                             | Scoped          | The three-path bootstrap orchestration (above)                                               |
| `HacmanApiClientFactory`                                           | Scoped          | Resolves the access token (cookie → session state) and builds the backend `HttpClient`       |
| `IUserApiService` / `IUserGalleryApiService` / `IScoresApiService` | Scoped          | Typed backend API clients (users, galleries, scores)                                         |
| `GalleryRouteResolver`                                             | (static helper) | Maps a gallery type name back to its `/book`·`/character`·`/cosplay` route                   |
| `ILocalStorageService`                                             | Scoped          | Thin JS-interop wrapper over the browser `localStorage` (`localstorage.js`)                  |

`ScoresApiService` fetches the three difficulties concurrently (`Task.WhenAll`) and authorizes each call with a per-request `HttpRequestMessage` (not shared `DefaultRequestHeaders`).

## Component layer

- [`Components/Layout/MainLayout.razor`](../Components/Layout/MainLayout.razor) — thin: registers the JS listener, delegates to `UserBootstrapService`, holds only the loading flag / error message / current user for rendering.
- [`Components/Shared/GalleryTypePage.razor`](../Components/Shared/GalleryTypePage.razor) — the shared skeleton behind `/book`, `/character`, `/cosplay`; parametrized by route segment, config key, default group name, and title. The three page files are ~10-line shells over it.
- [`Components/Shared/GalleryCard.razor`](../Components/Shared/GalleryCard.razor) — a single gallery tile: unlock state, image variant + `missing.jpg` fallback, best time, and VIEW/RANKINGS/PLAY actions.
- Pages: `Home`, `Gallery` (detail + lightbox + rankings), `Scores`, `Theme`.

## Key configuration

Loaded in priority order: **User Secrets → Environment Variables → `appsettings.{Environment}.json` → `appsettings.json`**.

| Key                                                                   | Purpose                                                                     |
|-----------------------------------------------------------------------|-----------------------------------------------------------------------------|
| `AppBasePath`                                                         | Base path prefix for routing/cookies (e.g. `/hacman/app`)                   |
| `HacmanApiBaseUrl`                                                    | Public (browser-facing) backend API base URL                                |
| `HacmanApiInternalUrl`                                                | Optional server-to-server backend URL; falls back to `HacmanApiBaseUrl`     |
| `HacmanGameUrl`                                                       | URL to the Hacman game (used by `GalleryCard.Play`)                         |
| `HacmanResourcesUrl`                                                  | Base URL for gallery image resources                                        |
| `MaxRedirectAttempts`                                                 | Max login redirect attempts (default: 2)                                    |
| `NebulaApi:BaseUrl` / `NebulaApi:LoginUrl`                            | Nebula gateway base and login/redirect URLs                                 |
| `NEBULA_CLIENT_ID` / `NEBULA_CLIENT_SECRET` / `NEBULA_TOKEN_ENDPOINT` | Nebula token-exchange credentials/endpoint                                  |
| `HacmanDictionary:{Book,Character,Cosplay}GroupName`                  | Display-name mapping for each gallery type                                  |
| `TrustSelfSignedCertificates`                                         | Opt into trusting self-signed/private-CA upstream certs outside Development |

## Security notes

- **Scoped, not singleton, for per-user state.** `UserSessionState` (holds the JWT) and `UserGalleriesState` (holds per-user galleries) are registered **scoped** so each Blazor Server circuit gets its own instance. Registering either as a singleton leaks one user's data — including the access token — to every connected user. (This was a real bug, fixed in the v2.0.0 refactor; a DI-registration regression test guards it.)
- **Tokens are HTTP-only cookies**, never exposed to page JavaScript. `app.js` sends them implicitly via `credentials: 'include'`.
- **`DangerousAcceptAnyServerCertificateValidator` is gated** behind Development or `TrustSelfSignedCertificates=true` — it is not applied unconditionally. Set the flag on deployments whose upstreams use self-signed/private-CA certificates (e.g. a Traefik-fronted homelab).
- Request logging uses the built-in `UseHttpLogging` (which redacts unlisted header values); the old debug middleware that logged the full `Cookie:` header on every request has been removed.

---
For the backend's architecture, see [`../../hacman-app-back/docs/ARCHITECTURE.md`](../../hacman-app-back/docs/ARCHITECTURE.md).
