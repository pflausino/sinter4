---
inclusion: always
---

# Padrões do Projeto

## Stack Tecnológica

- **.NET 10** — Framework principal (backend e frontend)
- **Blazor Server** — Frontend
- **PostgreSQL** — Banco de dados relacional
- **Firebase Authentication** — Autenticação e gerenciamento de identidade
- **xUnit** — Framework de testes automatizados

## Arquitetura

- **Mono-repo** — Todo o código (backend, frontend, testes, infra) vive em um único repositório
- **Estrutura simples e direta** — Evitar over-engineering, abstrações desnecessárias e camadas excessivas
- Preferir convenção sobre configuração
- Manter a árvore de pastas rasa e previsível

### Estrutura de Pastas

```
/src
  /Api            → Web API (Minimal APIs) e endpoints
  /Web            → Projeto Blazor Server (frontend)
  /Domain         → Entidades, enums, value objects
  /Infrastructure → Acesso a dados (EF Core), Firebase, migrations
  /Shared         → DTOs, contratos, atributos de validação
/tests
  /Api.Tests      → Testes da API (xUnit, FsCheck, WebApplicationFactory)
  /Web.Tests      → Testes de componentes Blazor (bUnit, NSubstitute)
  /Integration    → Testes de integração (Testcontainers PostgreSQL)
```

## Banco de Dados — PostgreSQL

- Usar **Entity Framework Core** com provider Npgsql
- Migrations versionadas no repositório (`dotnet ef migrations`)
- Nomear tabelas e colunas em **snake_case** (convenção PostgreSQL)
- Sempre usar tipos apropriados: `uuid` para PKs, `timestamptz` para datas
- Índices explícitos para colunas usadas em filtros e joins
- Evitar stored procedures — lógica fica no código C#

## Autenticação — Firebase

- Validar tokens JWT do Firebase no backend via middleware ASP.NET Core
- Usar `FirebaseAdmin` SDK para operações administrativas
- Claims customizadas para controle de roles/permissões
- Nunca armazenar senhas localmente — Firebase é a fonte de verdade
- Proteger endpoints com `[Authorize]` e policies quando necessário

## Blazor — Frontend

- Componentes pequenos e focados (single responsibility)
- Usar `@inject` para DI nos componentes
- Separar lógica em code-behind (`.razor.cs`) quando o componente crescer
- Gerenciamento de estado simples — cascading parameters ou serviços scoped
- Formulários com `EditForm` e `DataAnnotations` para validação

## Boas Práticas de Código

- **Nomenclatura**: PascalCase para classes/métodos, camelCase para variáveis locais
- **Nullable reference types** habilitados (`<Nullable>enable</Nullable>`)
- **Async/await** em toda operação I/O
- **Injeção de dependência** — registrar serviços no DI container, nunca instanciar manualmente
- **Logging estruturado** com `ILogger<T>` e placeholders (`{UserId}`, não interpolação)
- **Tratamento de erros**: usar middleware global de exceções, Result pattern para fluxos esperados
- **Configuração**: usar `appsettings.json` + variáveis de ambiente, nunca hardcode
- **Secrets**: nunca commitar secrets — usar User Secrets em dev, variáveis de ambiente em prod
- Manter métodos curtos (< 30 linhas idealmente)
- Preferir composição sobre herança
- SOLID, mas sem fanatismo — pragmatismo acima de purismo

## Testes Automatizados — xUnit

- Todo código de negócio deve ter cobertura de testes
- Nomenclatura: `MetodoSobTeste_Cenario_ResultadoEsperado`
- Usar **Arrange / Act / Assert** (AAA pattern)
- Mocks com **NSubstitute**
- **FsCheck** para testes baseados em propriedades (property-based testing)
- **bUnit** para testes de componentes Blazor
- Testes de integração com `WebApplicationFactory<T>` e banco em container (Testcontainers)
- Testes devem ser independentes — sem dependência de ordem ou estado compartilhado
- Rodar testes com `make test` ou `dotnet test` na raiz do repositório

## Comandos Úteis

O projeto usa um `Makefile` para workflows de desenvolvimento:

```bash
make restore          # Restaurar dependências NuGet
make build            # Build da solution inteira
make test             # Rodar todos os testes
make db               # Subir PostgreSQL via Docker Compose
make api              # Rodar a API (sobe o banco antes)
make web              # Rodar o Blazor frontend
make dev              # Build + API + Web em paralelo
make migrate          # Aplicar migrations do EF Core
make migration name=X # Criar nova migration
make clean            # Limpar artefatos de build
```

Comandos `dotnet` diretos também funcionam:

```bash
dotnet restore
dotnet build
dotnet test
dotnet ef database update --project src/Infrastructure --startup-project src/Api
dotnet run --project src/Api
dotnet run --project src/Web
```

## Regras para o Agente

- Sempre gerar código em C# idiomático e moderno (.NET 10)
- Respeitar a estrutura mono-repo existente
- Não criar camadas ou abstrações que não foram pedidas
- Ao criar endpoints, usar Minimal APIs (padrão do projeto)
- Ao criar componentes Blazor, seguir o padrão dos componentes existentes
- Sempre incluir tratamento de erros adequado
- Código gerado deve compilar sem warnings
- Preferir soluções simples e legíveis sobre soluções "elegantes" e complexas
