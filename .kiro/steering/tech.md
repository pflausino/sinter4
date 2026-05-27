# Tech Stack

## Core

- **.NET 10** — Backend and frontend runtime
- **Blazor** — Frontend (Server or WebAssembly)
- **ASP.NET Core** — Web API (Minimal APIs or Controllers)
- **Entity Framework Core** — ORM with Npgsql provider
- **PostgreSQL** — Database
- **Firebase Authentication** — Identity and auth (JWT validation + FirebaseAdmin SDK)

## Testing

- **xUnit** — Test framework
- **NSubstitute** or **Moq** — Mocking
- **Testcontainers** — Integration tests with real PostgreSQL
- **WebApplicationFactory<T>** — API integration tests

## Common Commands

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests
dotnet test

# Run EF migrations
dotnet ef database update --project src/Infrastructure

# Run API
dotnet run --project src/Api

# Run Blazor frontend
dotnet run --project src/Web
```
