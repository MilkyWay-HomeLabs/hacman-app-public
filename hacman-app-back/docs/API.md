# API Reference

Base path: `/api/v1/`. Interactive docs (Swagger UI) are available at `/swagger/index.html` when running in development.

All authenticated endpoints expect a JWT bearer token (set as the `accessToken` HTTP-only cookie by the frontend's
Nebula login flow, or sent as an `Authorization: Bearer <token>` header directly against the API). The token's `sub`
claim is parsed as `"authId,email"` by `AuthClaimsParser` to resolve the calling `User`.

## Contents

- [Users](#users)
- [User Galleries](#user-galleries)
- [Level Scores](#level-scores)
- [Maze Levels](#maze-levels)
- [Gallery Catalog](#gallery-catalog)
- [Common error shapes](#common-error-shapes)

---

## Users

### `GET /api/v1/users/{id}`

Retrieve a user by internal GUID.

- **Auth:** none
- **200** → `User` object
- **404** → user not found

### `GET /api/v1/users/external-auth-id`

Retrieve the authenticated user, resolved from the JWT.

- **Auth:** required
- **200** → `User` object
- **401** → invalid/missing `sub` claim
- **404** → no user mapped to this identity yet

### `POST /api/v1/users`

Create a user account for the authenticated identity. Also seeds one `UserGallery` (locked) per existing gallery,
atomically.

- **Auth:** required
- **Body:**
  ```json
  { "username": "string (required)" }
  ```
- **201** → created `User`, `Location` header pointing at `GET /api/v1/users/{id}`
- **400** → missing username, or the identity/username is already in use
- **401** → invalid/missing `sub` claim

`User` shape:

```json
{
  "id": "guid",
  "externalAuthId": "string",
  "username": "string",
  "email": "string",
  "createdAt": "datetime",
  "updatedAt": "datetime"
}
```

---

## User Galleries

### `GET /api/v1/user-galleries`

Returns gallery unlock/progress state. Resolution order:

1. If `galleryId` is provided → the single association for that gallery (any user — see note below).
2. Else if `userId` is provided → all gallery associations for that user.
3. Else → all gallery associations for the **authenticated** user (derived from the JWT).

- **Auth:** required (only when neither query param is supplied)
- **Query params:** `userId` (guid, optional), `galleryId` (guid, optional; takes priority)
- **200** → `UserGallery` object or array, depending on which mode was used
- **401** → invalid/missing `sub` claim, or no user mapped to this identity

> The `galleryId` lookup path is `LIMIT 1` with no `ORDER BY` and no user filter — it returns *an* association for that
> gallery, not necessarily one belonging to a specific user. Prefer the `userId` or authenticated-user paths when the
> caller matters.

`UserGallery` shape:

```json
{
  "id": "guid",
  "userId": "guid",
  "galleryId": "guid",
  "gallery": {
    "...Gallery object, see Gallery Catalog..."
  },
  "isUnlocked": true,
  "unlockedAt": "datetime|null",
  "bestTimeMs": "long|null",
  "bestTimeAt": "datetime|null",
  "createdAt": "datetime",
  "updatedAt": "datetime"
}
```

---

## Level Scores

### `GET /api/v1/level-scores`

Paginated score listing for a gallery, most recent leaderboard first (ordered by `timeMs` ascending).

- **Auth:** none
- **Query params:** `galleryId` (guid, required), `difficulty` (string, optional — `EASY`/`MEDIUM`/`HARD`),
  `pageNumber` (int, default `1`), `pageSize` (int, default `10`)
- **200** → array of `ScoreEntryDto`
- **500** → unexpected error

### `GET /api/v1/level-scores/user/{userId}`

All scores recorded by a specific user, optionally filtered.

- **Auth:** none
- **Path param:** `userId` (guid, required, non-empty)
- **Query params:** `galleryId` (guid, optional), `difficulty` (string, optional)
- **200** → array of `ScoreEntryDto`
- **400** → `userId` is empty
- **500** → unexpected error

### `POST /api/v1/level-scores`

Records a new score for the authenticated user. If this is a new personal best for that gallery, also updates (and
unlocks, if not already) the cached `UserGallery.bestTimeMs`/`bestTimeAt` — both writes happen inside a single
transaction (`SharedKernel.IUnitOfWork`).

- **Auth:** required
- **Body:**
  ```json
  {
    "galleryId": "guid (required)",
    "difficulty": "string (required — EASY | MEDIUM | HARD)",
    "timeMs": "long (required, > 0)"
  }
  ```
- **201** → created `ScoreEntryDto`
- **400** → missing `galleryId`/`difficulty`, non-positive `timeMs`, or an invalid `difficulty` value
- **401** → invalid/missing `sub` claim, or no user mapped to this identity
- **500** → unexpected error

`ScoreEntryDto` shape:

```json
{
  "username": "string",
  "timeMs": "long",
  "achievedAt": "datetime",
  "difficulty": "string"
}
```

---

## Maze Levels

### `GET /api/v1/maze/levels/{galleryId}`

Returns all maze level templates for a gallery (one per difficulty).

- **Auth:** none
- **Path param:** `galleryId` (guid, required)
- **200** → array of `MazeLevelDto`
- **500** → unexpected error

`MazeLevelDto` shape (abridged — see `Modules/LevelTemplate/Api/MazeLevelDto.cs` for the full field list):

```json
{
  "id": "int",
  "galleryId": "guid",
  "difficulty": "string",
  "width": "short",
  "height": "short",
  "cells": "short[][]",
  "playerStartX": "short",
  "playerStartY": "short",
  "playerDirection": "string",
  "dots": "object",
  "enemies": "object",
  "buffs": "object",
  "debuffs": "object"
}
```

---

## Gallery Catalog

### `GET /api/v1/galleries/types`

All gallery type names.

- **Auth:** none
- **200** → `string[]`

### `GET /api/v1/galleries/themes`

All gallery theme names.

- **Auth:** none
- **200** → `string[]`

### `GET /api/v1/galleries/subtypes`

All gallery subtype names.

- **Auth:** none
- **200** → `string[]`

`Gallery` shape (as embedded in `UserGallery.gallery`):

```json
{
  "id": "guid",
  "title": "string",
  "description": "string|null",
  "imageCount": "int",
  "galleryId": "int",
  "theme": {
    "name": "string"
  },
  "type": {
    "name": "string"
  },
  "subtype": {
    "name": "string"
  },
  "level": "int",
  "createdAt": "datetime"
}
```

---

## Common error shapes

Most `400`/`401` responses use a `{ "message": "..." }` envelope:

```json
{
  "message": "Invalid token claims: sub is missing or invalid"
}
```

`500` responses return a plain string body:

```
"An unexpected error occurred."
```

See [`ARCHITECTURE.md#error-handling`](ARCHITECTURE.md#error-handling) for the full status-code convention.
