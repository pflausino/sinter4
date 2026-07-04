# Project Structure

Mono-repo with flat, predictable folder layout.

```
/src
  /Api            — Web API (Minimal APIs) and endpoints
  /Web            — Blazor Server frontend
  /Domain         — Entities, enums, value objects
  /Infrastructure — Data access (EF Core), Firebase, migrations
  /Shared         — DTOs, contracts, validation attributes
/tests
  /Api.Tests      — API unit and integration tests (xUnit, FsCheck, WebApplicationFactory)
  /Web.Tests      — Blazor component tests (bUnit, NSubstitute)
  /Integration    — Integration tests with real PostgreSQL (Testcontainers)
```

## Conventions

- Tables and columns in **snake_case** (via EFCore.NamingConventions)
- PKs use `uuid`, dates use `timestamptz`
- No stored procedures — logic stays in C#
- Blazor components use code-behind (`.razor.cs`) when they grow beyond trivial
- Configuration via `appsettings.json` + environment variables
- Secrets via User Secrets (dev) or env vars (prod)
- PostgreSQL 16 via Docker Compose for local development
