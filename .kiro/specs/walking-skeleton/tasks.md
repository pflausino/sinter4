# Plano de Implementação: Walking Skeleton

## Visão Geral

Implementação incremental da estrutura mínima funcional do mono-repo SinterPrints em .NET 10. Cada tarefa constrói sobre a anterior, culminando em um skeleton completo onde API, Blazor, PostgreSQL e testes se comunicam de ponta a ponta.

## Tarefas

- [x] 1. Criar solution file e scaffolding de todos os projetos
  - [x] 1.1 Criar o arquivo `SinterPrints.sln` na raiz do repositório
    - Criar solution file vazio
    - _Requirements: 1.1_
  - [x] 1.2 Criar os projetos `src/Domain/Domain.csproj`, `src/Shared/Shared.csproj`, `src/Infrastructure/Infrastructure.csproj`, `src/Api/Api.csproj` e `src/Web/Web.csproj`
    - Domain: class library net10.0, sem dependências
    - Shared: class library net10.0, referência para Domain
    - Infrastructure: class library net10.0, referência para Domain, pacotes Npgsql.EntityFrameworkCore.PostgreSQL e Microsoft.EntityFrameworkCore.Design
    - Api: web project net10.0, referências para Infrastructure e Shared, pacote Microsoft.AspNetCore.OpenApi
    - Web: blazor project net10.0, referência para Shared
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.7_
  - [x] 1.3 Criar os projetos de teste `tests/Api.Tests/Api.Tests.csproj` e `tests/Integration/Integration.csproj`
    - Api.Tests: xunit project net10.0, referência para Api, pacotes Microsoft.AspNetCore.Mvc.Testing e Microsoft.EntityFrameworkCore.InMemory
    - Integration: xunit project net10.0, pacote Testcontainers.PostgreSql
    - _Requirements: 1.1, 1.6, 1.7_
  - [x] 1.4 Adicionar todos os projetos ao solution file
    - Usar `dotnet sln add` para cada projeto
    - Organizar em solution folders `src` e `tests`
    - _Requirements: 1.1_

- [x] 2. Configurar Docker Compose
  - [x] 2.1 Criar `docker-compose.yml` na raiz do repositório
    - Serviço `postgres` com imagem `postgres:16`
    - Porta `5432:5432`
    - Variáveis: POSTGRES_DB=sinterprints, POSTGRES_USER=sinterprints, POSTGRES_PASSWORD=sinterprints_dev
    - Volume nomeado `pgdata` em `/var/lib/postgresql/data`
    - _Requirements: 4.1, 4.2, 4.3_

- [x] 3. Implementar camada de infraestrutura
  - [x] 3.1 Criar `src/Infrastructure/Data/AppDbContext.cs`
    - Herdar de DbContext
    - Configurar snake_case no OnModelCreating usando HasDefaultSchema e convenções Npgsql
    - _Requirements: 3.1, 3.2_
  - [x] 3.2 Criar `src/Infrastructure/DependencyInjection.cs` com método de extensão `AddInfrastructure`
    - Registrar AppDbContext com provider Npgsql
    - Ler connection string de `IConfiguration` (chave "DefaultConnection")
    - _Requirements: 3.3, 3.4_

- [x] 4. Implementar camada Shared
  - [x] 4.1 Criar `src/Shared/Dtos/HealthCheckResponse.cs`
    - Record com propriedades `Status` (string) e `Timestamp` (DateTime)
    - _Requirements: 2.2, 2.5_

- [x] 5. Implementar projeto API
  - [x] 5.1 Criar `src/Api/Program.cs` com configuração mínima
    - Registrar serviços de infraestrutura via `AddInfrastructure`
    - Configurar pipeline de request
    - _Requirements: 2.1, 3.4_
  - [x] 5.2 Criar `src/Api/Endpoints/HealthEndpoints.cs` com endpoint GET /health
    - Verificar conexão com banco via AppDbContext (CanConnectAsync)
    - Retornar HealthCheckResponse com status "Healthy" ou "Unhealthy"
    - Sempre retornar HTTP 200 independente do estado do banco
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_
  - [x] 5.3 Criar `src/Api/appsettings.json` e `src/Api/appsettings.Development.json`
    - Connection string apontando para o PostgreSQL do Docker Compose
    - _Requirements: 3.3_

- [x] 6. Implementar testes da API
  - [x] 6.1 Criar `tests/Api.Tests/HealthEndpointTests.cs`
    - Usar WebApplicationFactory<Program> para instanciar a API in-process
    - Substituir DbContext por provider InMemory para isolamento
    - Teste: GET /health retorna 200 OK
    - Teste: resposta contém JSON com campos `status` e `timestamp`
    - Teste: status é "Healthy" quando banco (InMemory) está disponível
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 7. Checkpoint — Verificar build e testes
  - Executar `dotnet build` na raiz e garantir que compila sem erros
  - Executar `dotnet test` na raiz e garantir que todos os testes passam
  - Ensure all tests pass, ask the user if questions arise.
  - _Requirements: 7.1, 7.2_

- [x] 8. Implementar projeto Blazor Web
  - [x] 8.1 Criar `src/Web/Program.cs` com configuração mínima do Blazor
    - Registrar HttpClient apontando para a URL da API
    - Configurar Blazor Server ou WebAssembly (conforme padrão do projeto)
    - _Requirements: 5.2_
  - [x] 8.2 Criar página `src/Web/Pages/Health.razor` consumindo o endpoint de health-check
    - Rota: `/health`
    - Fazer GET para a API e exibir o status retornado
    - Tratar erro de conexão com mensagem amigável
    - _Requirements: 5.1, 5.3_

- [x] 9. Checkpoint final — Verificar build completo e testes
  - Executar `dotnet build` na raiz e garantir que compila sem erros (incluindo Web)
  - Executar `dotnet test` na raiz e garantir que todos os testes passam
  - Ensure all tests pass, ask the user if questions arise.
  - _Requirements: 7.1, 7.2_

## Notas

- Linguagem de implementação: C# (.NET 10, TFM `net10.0`)
- Todos os projetos usam nullable reference types habilitados
- Convenção snake_case para banco de dados PostgreSQL
- Docker Compose é pré-requisito para testes de integração com banco real
- Os testes unitários (Api.Tests) usam InMemory provider para não depender de Docker
- Não há property-based tests neste skeleton (sem lógica de negócio que justifique PBT)
