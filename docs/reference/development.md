# Desenvolvimento Local

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Docker + Docker Compose (PostgreSQL local e testes de integração)
- `dotnet-ef` (`dotnet tool install --global dotnet-ef`) para migrations
- Projeto Firebase com Authentication (e-mail/senha) habilitado e o JSON da service account

## Setup inicial

```bash
git clone <repo> && cd SinterPrints4
make restore

# Secrets do Firebase (ver docs/firebase-setup.md)
dotnet user-secrets set "Firebase:ProjectId" "<project-id>" --project src/Api
dotnet user-secrets set "Firebase:ServiceAccountPath" "/caminho/fora/do/repo/sa.json" --project src/Api
dotnet user-secrets set "Firebase:ApiKey" "<web-api-key>" --project src/Web

make db        # sobe o PostgreSQL
make migrate   # aplica migrations
make dev       # build + API + Web (Web com dotnet watch)
```

- API: `http://localhost:5062` (perfil `http`)
- Web: `http://localhost:5105` (perfil `http`; `https://localhost:7279` no perfil `https`)

## Comandos `make`

| Comando | Faz |
| --- | --- |
| `make help` | Lista os comandos (default). |
| `make restore` | `dotnet restore`. |
| `make build` | Build da solution. |
| `make db` / `make db-stop` | Sobe/para o PostgreSQL (Docker Compose). |
| `make logs` | Logs do PostgreSQL. |
| `make api` | Sobe o db e roda a API. |
| `make web` | Roda o Web. |
| `make dev` | Sobe o db, faz build e roda API e Web em paralelo. |
| `make dev-stop` | Mata processos órfãos de `dotnet run`/`dotnet watch`. |
| `make migrate` | Aplica migrations. |
| `make migration name=X` | Cria migration `X`. |
| `make test` / `make test-watch` | Roda testes / modo watch. |
| `make clean` | `dotnet clean`. |

## Estrutura de pastas

```
src/
  Api/             Minimal API: Endpoints/, Filters/, Services/, Program.cs
  Web/             Blazor Server: Components/{Layout,Pages}, Services/, wwwroot/
  Domain/          Entities/, Enums/
  Infrastructure/  Data/, Auth/, Migrations/ (+ Scripts/*.sql)
  Shared/          Dtos/, Validation/
tests/
  Api.Tests/       Endpoints e serviços (WebApplicationFactory + EF InMemory)
  Web.Tests/       Componentes Blazor (bUnit) e serviços
  Integration/     PostgreSQL real (Testcontainers)
deploy/            SQL de import legado + README da VPS
docs/              Esta documentação e guias
```

## Convenções de código

- C# moderno, nullable habilitado, 4 espaços.
- PascalCase em tipos e membros públicos, camelCase em locais, sufixo `Async`.
- Componentes Blazor pequenos; lógica não trivial em `.razor.cs`.
- Apenas Minimal APIs (sem Controllers); DI em vez de `new`.
- Logging estruturado: `ILogger<T>` com placeholders.
- Commits: `type(scope): resumo imperativo` (ex.: `feat(file-records): ...`).

## Adicionando um campo à ficha (exemplo de fluxo)

1. Propriedade em `Domain/Entities/FileRecord.cs`.
2. Mapeamento em `Infrastructure/Data/AppDbContext.cs`.
3. `make migration name=AddCampoX` e revisar o SQL gerado.
4. DTOs em `Shared/Dtos/` (request + response) com validação.
5. `FileRecordService` (create/update/`ToResponse`, e `ValidSortFields` se for ordenável).
6. Formulários e tabela no Web.
7. Testes em `Api.Tests`, `Web.Tests` e, se tocar SQL, `Integration`.
