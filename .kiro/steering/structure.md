# Project Structure

Mono-repo with flat, predictable folder layout.

```
/src
  /Api            — Web API and endpoints
  /Web            — Blazor frontend
  /Domain         — Entities, enums, value objects
  /Infrastructure — Data access (EF Core), external services
  /Shared         — DTOs, contracts, shared extensions
/tests
  /Api.Tests      — API unit tests
  /Web.Tests      — Frontend tests
  /Domain.Tests   — Domain logic tests
  /Integration    — Integration tests (Testcontainers)
```

## Conventions

- Tables and columns in **snake_case** (PostgreSQL convention)
- PKs use `uuid`, dates use `timestamptz`
- No stored procedures — logic stays in C#
- Blazor components use code-behind (`.razor.cs`) when they grow beyond trivial
- Configuration via `appsettings.json` + environment variables
- Secrets via User Secrets (dev) or env vars (prod)
