# Testing

## Contents

- [Test projects & frameworks](#test-projects--frameworks)
- [Unit vs. integration](#unit-vs-integration)
- [Running tests](#running-tests)
- [Fixture conventions](#fixture-conventions)
- [Current coverage snapshot](#current-coverage-snapshot)
- [Coverage by module](#coverage-by-module)
- [Writing a new test](#writing-a-new-test)

## Test projects & frameworks

All backend tests live in `hacman-app-back.Tests/`, a single test project referencing `hacman-app-back`. Two test
frameworks coexist by convention:

| Framework                                  | Used for                                                            |
|--------------------------------------------|---------------------------------------------------------------------|
| **MSTest** (`[TestClass]`, `[TestMethod]`) | Repository-layer integration tests and some application-layer tests |
| **xUnit** (`[Fact]`)                       | Most service- and controller-layer unit tests                       |

Mocking is done with **Moq**. Integration tests target a real PostgreSQL instance — either a long-lived local
`milky-test-postgres` Docker container, or a Testcontainers-provisioned instance in CI.

## Unit vs. integration

Tests are categorized by the **`[TestCategory("Integration")]`** MSTest trait — not by folder location. Every class
inheriting `RepositoryTestBase` carries this attribute. This is deliberately framework/location-agnostic: the physical
test layout mirrors the backend's bounded contexts (
`hacman-app-back.Tests/Modules/<Context>/{Domain,Infrastructure,Application,Api}/`), not a unit/integration split.

Two files intentionally live outside `Modules/`:

- `hacman-app-back.Tests/repository/RepositoryTestBase.cs` — shared base class providing `GetConnectionString()` and DB
  lifecycle helpers.
- `hacman-app-back.Tests/SharedKernel/UnitOfWorkTest.cs` — tests `SharedKernel.IUnitOfWork`'s guard clauses without a DB
  connection; correctly carries no `[TestCategory("Integration")]`.

> **Filter trait name matters.** The correct trait key is `TestCategory`, not `Category`. Filtering with
`--filter "Category!=Integration"` silently matches *every* test (MSTest's `[TestCategory]` attribute exposes the trait
> as `TestCategory`, not `Category`) — always use `TestCategory!=Integration` / `TestCategory=Integration`.

## Running tests

```bash
# All tests (unit + integration)
dotnet test ./hacman-app.sln

# Backend test project only
dotnet test ./hacman-app-back.Tests/hacman-app-back.Tests.csproj

# Unit tests only — no DB required
dotnet test ./hacman-app-back.Tests/hacman-app-back.Tests.csproj --filter "TestCategory!=Integration"

# Integration tests only — needs a reachable PostgreSQL instance
export TEST_HACMAN_CONNECTIONSTRING="Host=localhost;Database=test_hacman;Username=hacman;Password=<your-test-db-password>"
dotnet test ./hacman-app-back.Tests/hacman-app-back.Tests.csproj --filter "TestCategory=Integration"

# A single test class
dotnet test ./hacman-app-back.Tests/hacman-app-back.Tests.csproj --filter "FullyQualifiedName~UserGalleriesControllerTest"
```

Integration tests provision/target a real Postgres database via `TEST_HACMAN_CONNECTIONSTRING`. Locally this is commonly
a long-lived `milky-test-postgres` container (see `hacman-app-back/.env`); `RepositoryTestBase` does not auto-provision
a database — it expects one to already exist and be seeded (galleries, lookup values, etc.).

## Fixture conventions

Integration tests follow a **self-contained fixture** pattern established across the repository-layer test suites:

1. **Create your own test data** in the test method (or a small `[TestInitialize]`/private helper), rather than relying
   on pre-seeded rows that may or may not exist.
2. **Assert unconditionally.** Avoid `if (x != null) { assert }` — a test that silently passes when its precondition
   isn't met proves nothing.
3. **Clean up in `[TestCleanup]`.** The common pattern is to tag fixture rows with `external_auth_id LIKE 'test_%'` and
   delete matching `users` rows in cleanup; rely on `ON DELETE CASCADE` where the schema already provides it (e.g.
   `user_galleries`/`level_scores` cascade from `users`) instead of writing redundant cleanup queries.
4. **Check the real schema before writing fixture SQL.** Constraints that matter (CHECK ranges, nullable columns, unique
   constraints, cascade behavior) aren't always obvious from repository code alone:
   ```bash
   docker exec milky-test-postgres psql -U hacman -d test_hacman -c "\d <table>"
   ```

Example (`UserGalleryRepositoryTest.cs`):

```csharp
[TestCleanup]
public async Task Cleanup()
{
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(
        "DELETE FROM users WHERE external_auth_id LIKE 'test_%'", conn);
    await cmd.ExecuteNonQueryAsync();
}
```

## Current coverage snapshot

|                                                | Count |
|------------------------------------------------|-------|
| Total tests                                    | 143   |
| Unit tests (`TestCategory!=Integration`)       | 96    |
| Integration tests (`TestCategory=Integration`) | 47    |

Every repository class under `Modules/*/Infrastructure/` and `SharedKernel/` has at least one integration test file, and
every public method that mutates or reads meaningful state has dedicated coverage — including the transactional
guarantee that `SharedKernel.UnitOfWork` exists to provide (see
`UserUpdaterIntegrationTest.CreateNewUserData_RollsBackEverything_WhenUserGalleryCreationFailsPartway`).

## Coverage by module

| Context        | Test files                                                                                                                                                                                                                                                                                                              |
|----------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Identity       | `AuthClaimsParserTest`, `UsersControllerTest`, `UsersProviderTest`, `UserUpdaterTest` (unit), `UserUpdaterIntegrationTest` (integration), `UserRepositoryTest` (integration)                                                                                                                                            |
| PlayerProgress | `UserGalleriesControllerTest`, `UserGalleryProviderTest` (unit), `UserGalleryRepositoryTest`, `UserGalleryRepositoryGetAllTest` (integration)                                                                                                                                                                           |
| Scoring        | `LevelScoreControllerTest`, `LevelScoreServiceTest` (unit), `LevelScoreRepositoryTest` (integration)                                                                                                                                                                                                                    |
| LevelTemplate  | `MazeLevelControllerTest`, `MazeLevelProviderTest`, `MazeLevelTest` (unit — domain invariants), `MazeLevelRepositoryTest` (integration)                                                                                                                                                                                 |
| GalleryCatalog | `GalleryTypesControllerTest`, `GalleryThemesControllerTest`, `GallerySubtypesControllerTest`, `GalleryTypeServiceTest`, `GalleryThemeServiceTest`, `GallerySubtypeServiceTest` (unit), `GalleryRepositoryTest`, `GalleryTypeRepositoryTest`, `GalleryThemeRepositoryTest`, `GallerySubtypeRepositoryTest` (integration) |
| SharedKernel   | `UnitOfWorkTest` (unit — guard clauses), `UnitOfWorkIntegrationTest` (integration — real `BeginAsync`/`CommitAsync`/`RollbackAsync` against a live connection)                                                                                                                                                          |

## Writing a new test

1. **Repository-layer change → integration test is required.** Add it to the matching
   `Modules/<Context>/Infrastructure/*RepositoryTest.cs`, following the self-contained fixture pattern above.
2. **Service/controller-layer change → unit test with mocked dependencies.** Follow the existing `Mock<I*>` + `Assert`
   style already used in the sibling test file for that layer.
3. Never depend on externally pre-seeded data unless the test is explicitly asserting something about the seed itself (
   e.g. `GalleryRepositoryTest`'s hard-coded gallery-count checks).
4. Run the relevant filtered `dotnet test` command locally before committing — don't rely on CI to catch a broken build,
   since CI in this repo does not gate PR merges.
