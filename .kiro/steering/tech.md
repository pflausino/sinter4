# Tech Stack

## Core

- **.NET 10** — Backend and frontend runtime
- **Blazor Server** — Frontend (interactive server-side rendering)
- **ASP.NET Core** — Web API (Minimal APIs)
- **Entity Framework Core** — ORM with Npgsql provider + EFCore.NamingConventions
- **PostgreSQL 16** — Database (via Docker Compose)
- **Firebase Authentication** — Identity and auth (JWT validation + FirebaseAdmin SDK)

## Testing

- **xUnit** — Test framework
- **FsCheck / FsCheck.Xunit** — Property-based testing
- **bUnit** — Blazor component testing
- **NSubstitute** — Mocking
- **Testcontainers.PostgreSql** — Integration tests with real PostgreSQL
- **WebApplicationFactory<T>** — API integration tests
- **coverlet** — Code coverage collection

## Common Commands

The project uses a `Makefile` for development workflows:

```bash
make restore          # Restore NuGet packages
make build            # Build the full solution
make test             # Run all tests
make db               # Start PostgreSQL via Docker Compose
make api              # Run the API (starts db first)
make web              # Run the Blazor frontend
make dev              # Build, then run API and Web together
make migrate          # Apply EF Core migrations
make migration name=X # Create a new migration
make clean            # Clean build artifacts
```

Direct `dotnet` commands also work:

```bash
dotnet restore
dotnet build
dotnet test
dotnet ef database update --project src/Infrastructure --startup-project src/Api
dotnet run --project src/Api
dotnet run --project src/Web
```
