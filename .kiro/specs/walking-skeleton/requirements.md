# Documento de Requisitos

## Introdução

Este documento define os requisitos para o Walking Skeleton do projeto SinterPrints — a estrutura mínima funcional que prova a comunicação entre todas as camadas do mono-repo .NET 10: API, frontend Blazor, banco de dados PostgreSQL e testes automatizados.

## Glossário

- **API**: Projeto ASP.NET Core Minimal API localizado em `src/Api`
- **Web**: Projeto Blazor localizado em `src/Web`
- **Infrastructure**: Projeto de acesso a dados com EF Core localizado em `src/Infrastructure`
- **Domain**: Projeto de domínio localizado em `src/Domain`
- **Shared**: Projeto de DTOs e contratos compartilhados localizado em `src/Shared`
- **AppDbContext**: Classe DbContext do Entity Framework Core que gerencia a conexão com PostgreSQL
- **Health-check**: Endpoint HTTP que reporta o estado de saúde da aplicação
- **HealthCheckResponse**: DTO que representa a resposta do endpoint de health-check
- **Solution_File**: Arquivo `.sln` que organiza todos os projetos do mono-repo
- **Docker_Compose**: Arquivo `docker-compose.yml` que define serviços de infraestrutura local
- **WebApplicationFactory**: Classe do ASP.NET Core para testes de integração in-process

## Requisitos

### Requisito 1: Estrutura da Solution e Projetos

**User Story:** Como desenvolvedor, quero que o mono-repo tenha uma solution file organizada com todos os projetos e referências corretas, para que eu possa compilar e navegar o código de forma consistente.

#### Critérios de Aceitação

1. THE Solution_File SHALL conter os projetos Api, Web, Domain, Infrastructure e Shared em `src/` e Api.Tests e Integration em `tests/`
2. WHEN o projeto Api é compilado, THE Solution_File SHALL resolver as referências para Infrastructure e Shared sem erros
3. WHEN o projeto Infrastructure é compilado, THE Solution_File SHALL resolver a referência para Domain sem erros
4. WHEN o projeto Shared é compilado, THE Solution_File SHALL resolver a referência para Domain sem erros
5. WHEN o projeto Web é compilado, THE Solution_File SHALL resolver a referência para Shared sem erros
6. WHEN o projeto Api.Tests é compilado, THE Solution_File SHALL resolver a referência para Api sem erros
7. THE Solution_File SHALL utilizar o TFM `net10.0` em todos os projetos

### Requisito 2: Endpoint de Health-Check

**User Story:** Como desenvolvedor, quero um endpoint de health-check na API, para que eu possa verificar que a aplicação está rodando e respondendo a requisições HTTP.

#### Critérios de Aceitação

1. WHEN uma requisição GET é feita para `/health`, THE API SHALL retornar HTTP 200 OK com corpo JSON
2. WHEN o endpoint de health-check responde, THE API SHALL incluir os campos `status` (string) e `timestamp` (DateTime UTC) no corpo JSON
3. WHEN o banco de dados está acessível, THE API SHALL retornar `status` com valor "Healthy"
4. WHEN o banco de dados está inacessível, THE API SHALL retornar `status` com valor "Unhealthy"
5. THE API SHALL serializar a resposta usando o DTO HealthCheckResponse definido no projeto Shared

### Requisito 3: Camada de Infraestrutura

**User Story:** Como desenvolvedor, quero que o AppDbContext esteja configurado com PostgreSQL e convenção snake_case, para que o acesso a dados funcione corretamente desde o início do projeto.

#### Critérios de Aceitação

1. THE Infrastructure SHALL registrar o AppDbContext no container de DI com provider Npgsql para PostgreSQL
2. THE AppDbContext SHALL aplicar convenção de nomenclatura snake_case para tabelas e colunas
3. WHEN a connection string é fornecida via configuração, THE Infrastructure SHALL utilizá-la para conectar ao PostgreSQL
4. THE Infrastructure SHALL expor um método de extensão `AddInfrastructure` em `IServiceCollection` para registro dos serviços

### Requisito 4: Docker Compose para Desenvolvimento Local

**User Story:** Como desenvolvedor, quero um Docker Compose configurado com PostgreSQL 16, para que eu possa rodar o banco de dados localmente sem instalação manual.

#### Critérios de Aceitação

1. THE Docker_Compose SHALL definir um serviço PostgreSQL 16 com porta 5432 exposta
2. THE Docker_Compose SHALL configurar o banco com nome `sinterprints`, usuário `sinterprints` e senha `sinterprints_dev`
3. THE Docker_Compose SHALL persistir dados do PostgreSQL via volume nomeado

### Requisito 5: Projeto Blazor Web

**User Story:** Como desenvolvedor, quero uma página mínima no Blazor que consuma o endpoint de health-check da API, para que eu prove que o frontend consegue se comunicar com o backend.

#### Critérios de Aceitação

1. WHEN o usuário navega para `/health` no Blazor, THE Web SHALL exibir uma página com o status retornado pela API
2. THE Web SHALL configurar um HttpClient para comunicação com a API
3. WHEN a API está inacessível, THE Web SHALL exibir uma mensagem de erro amigável ao usuário

### Requisito 6: Testes Automatizados

**User Story:** Como desenvolvedor, quero testes automatizados que validem o endpoint de health-check usando WebApplicationFactory, para que eu tenha confiança de que a API funciona corretamente.

#### Critérios de Aceitação

1. WHEN o teste é executado, THE Api.Tests SHALL usar WebApplicationFactory para criar uma instância in-process da API
2. WHEN o teste faz GET /health, THE Api.Tests SHALL verificar que a resposta é HTTP 200 OK
3. WHEN o teste faz GET /health, THE Api.Tests SHALL verificar que o corpo contém um JSON válido com campos `status` e `timestamp`

### Requisito 7: Verificação de Build

**User Story:** Como desenvolvedor, quero que `dotnet build` e `dotnet test` passem sem erros na raiz do repositório, para que eu tenha certeza de que o skeleton está íntegro.

#### Critérios de Aceitação

1. WHEN `dotnet build` é executado na raiz do repositório, THE Solution_File SHALL compilar todos os projetos sem erros
2. WHEN `dotnet test` é executado na raiz do repositório, THE Solution_File SHALL executar todos os testes e reportar sucesso
