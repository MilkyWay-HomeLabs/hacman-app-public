# Contributing

## Language

All code, identifiers, comments, commit messages, PR titles/descriptions, and documentation are written in **English**.

## Branching & commits

- Never commit directly to `main` or `dev`.
- Branch names: `feature/short-description`, `fix/short-description`, `chore/short-description`,
  `refactor/short-description`.
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `chore:`,
  `refactor:`, `test:`, `docs:`.
- PRs are self-merged (no required reviewers, no branch protection). CI (`.github/workflows/backend-cicd.yml`) triggers
  only on push to `dev` and does **not** gate PR merges — run `dotnet test` locally before merging.
- Merge strategy: regular merge commits (`Merge pull request #N`), not squash merges.

## Test-driven development

- Write a failing test first, then implement the minimum code to make it pass, then refactor.
- Every new feature or bug fix ships with a corresponding test.
- **Repository-layer changes require an integration test** against a real PostgreSQL instance. See [
  `docs/TESTING.md`](docs/TESTING.md) for conventions and how to run them.
- Never skip, comment out, or write a trivially-true test to make a suite pass.

## Observability

- Add structured logging (`ILogger<T>`) at service entry/exit for non-trivial operations, on errors, and around external
  calls.
- Expose Prometheus-compatible metrics where architecturally meaningful, using the existing middleware — don't introduce
  new infrastructure dependencies without discussion.

## Code review checklist

Before opening a PR, verify:

- [ ] Change stays within its intended scope — no drift into unrelated files/layers
- [ ] Layer boundaries respected (`Api` → `Application` → `Infrastructure` → `Domain`)
- [ ] No unrelated behavior change slipped in alongside the fix/feature
- [ ] Structured logging added at entry/exit and error points
- [ ] `dotnet build ./hacman-app.sln -c Release --no-restore` succeeds
- [ ] `dotnet test ./hacman-app.sln` passes, including new/changed tests

## Documentation & versioning

- Update [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) when the module layout, layering, or conventions change.
- Keep `appsettings.json`'s `App:Version` in sync with release-relevant changes.
- Document new public endpoints with Swagger XML comments (`/// <summary>`) and add them to [
  `docs/API.md`](docs/API.md).
- Add a [`CHANGELOG.md`](CHANGELOG.md) entry for anything user- or API-visible.

## Scope boundaries

- Work only within `hacman-app-back/`, `hacman-app-front/`, `hacman-app-back.Tests/`, or repository-level config files.
- Never modify `Dockerfile`, `docker-compose.yml`, `compose.yaml`, or other Docker infrastructure files.
- Never run database migrations, schema changes, or direct database operations outside of the test harness.
