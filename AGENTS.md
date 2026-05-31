# Repository Guidelines

## Project Structure & Module Organization

This is a .NET 10 mono-repo. Application code lives under `src/`: `src/Api` for ASP.NET Core endpoints, `src/Web` for the Blazor UI, `src/Infrastructure` for EF Core data access and external services, `src/Domain` for core types, and `src/Shared` for DTOs/contracts. Tests live under `tests/`: `Api.Tests`, `Web.Tests`, and `Integration`. Frontend static assets are in `src/Web/wwwroot`; deployment and setup notes live in `deploy/` and `docs/`.

## Build, Test, and Development Commands

Use the root `Makefile` for common workflows:

```bash
make restore          # restore NuGet packages
make build            # build the full solution
make test             # run all tests
make db               # start PostgreSQL via Docker Compose
make api              # run the API, starting db first
make web              # run the Blazor frontend
make dev              # build, then run API and Web together
make migrate          # apply EF Core migrations
make migration name=AddExampleTable
make clean            # clean build artifacts
```

Direct `dotnet` commands are also valid, for example `dotnet test tests/Web.Tests`.

## Coding Style & Naming Conventions

Use idiomatic modern C# with nullable reference types enabled. Use 4-space indentation for C# and Razor code. Prefer PascalCase for types, methods, components, and public members; camelCase for locals and parameters; and `Async` suffixes for asynchronous methods. Keep Blazor components small; move non-trivial logic into `.razor.cs` code-behind files. Use DI registrations instead of manual service construction. Database table and column names should follow PostgreSQL-style `snake_case`.

## Testing Guidelines

Tests use xUnit, FsCheck, bUnit, NSubstitute, `WebApplicationFactory<T>`, and Testcontainers PostgreSQL. Name test classes with `Tests` or `PropertyTests`, and use descriptive test names such as `MethodUnderTest_Scenario_ExpectedResult` when adding new coverage. Follow Arrange/Act/Assert. Add focused tests for auth, token handling, EF behavior, components, and any changed business logic. Run `make test` before opening a PR; use `dotnet test --collect:"XPlat Code Coverage"` when coverage data is needed.

## Commit & Pull Request Guidelines

Recent history uses short conventional-style commits such as `feat(web): ...`, `fix(docker): ...`, and `chore(tests): ...`. Prefer `type(scope): concise imperative summary`; keep the scope specific when useful. Pull requests should include a clear change summary, test results, linked issues, and screenshots or short recordings for visible UI changes.

## Security & Configuration Tips

Do not commit secrets, Firebase credentials, or local connection strings. Use `appsettings.Development.json` only for non-sensitive defaults, User Secrets for local secrets, and environment variables in production. Keep Docker Compose credentials development-only.
