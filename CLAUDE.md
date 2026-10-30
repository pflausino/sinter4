# CLAUDE.md

@AGENTS.md

O `AGENTS.md` (importado acima) tem as convenções gerais: estrutura, estilo, testes e commits. Este arquivo
acrescenta o que não está lá: como as peças conversam, armadilhas do código e o ambiente local.

## O que é

SinterPrints: catálogo de "fichas" de arquivos de arte final (CorelDRAW, Photoshop etc.) de uma gráfica,
boa parte vinda de acervo legado em disquete (`FlopDiskNumber`). Hoje o produto é um CRUD de `FileRecord`
com busca, ordenação e paginação, protegido por login Firebase. A UI é em pt-BR.

## Arquitetura (o que importa saber)

```
Browser ──SignalR──> Web (Blazor Server) ──HTTP + Bearer JWT──> Api (Minimal API) ──EF Core──> PostgreSQL
                       │
                       └──REST──> Firebase Identity Toolkit (login / refresh)
```

- **Web nunca acessa o banco.** `Web.csproj` referencia só `Domain` e `Shared`. Toda leitura/escrita passa
  pela API usando `AuthenticatedHttpClient` (HttpClient nomeado `"Api"` + header `Bearer` com o ID token).
- **Login:** `Login.razor` → `FirebaseTokenProvider` chama a REST API do Firebase com `Firebase:ApiKey` →
  ID/refresh token gravados em `ProtectedLocalStorage` (`ProtectedTokenStorage`) →
  `FirebaseAuthStateProvider` decodifica o JWT no servidor do Blazor. O estado de login só é avaliado quando
  o circuito começa, então quem mantém a sessão viva é `FirebaseTokenProvider.GetTokenAsync`: ele renova o
  token quando faltam ≤ 5 min. Sem token utilizável, `AuthenticatedHttpClient` limpa a sessão e manda para
  `/login`. Não use `DelegatingHandler` para isso: roda fora do escopo do circuito e não lê o
  `ProtectedLocalStorage`.
- **API:** valida o JWT contra `https://securetoken.google.com/{Firebase:ProjectId}`; todos os endpoints de
  `/api/file-records` exigem a policy `"Authenticated"`. Respostas 401 trazem `{ error, message }`
  (`missing_token`, `invalid_token`, `token_expired`).
- **Contratos** ficam em `src/Shared/Dtos` (records). Validação usa DataAnnotations nos parâmetros do
  construtor dos records + `ValidationFilter<T>`; atributos próprios em `src/Shared/Validation`
  (`NotWhiteSpace`, `NotDefaultDateTime`).

### Endpoints

| Método | Rota | Observação |
|---|---|---|
| GET | `/health` | sem auth |
| GET | `/api/me` | uid/email do token |
| GET | `/api/file-records?offset&limit&sortBy&sortDir` | `limit` 1–100 (padrão 50) |
| GET | `/api/file-records/search?q&offset&limit&sortBy&sortDir` | `q` ≤ 200 caracteres |
| GET | `/api/file-records/duplicates?name&excludeId` | até 5 fichas com o mesmo nome (aviso no cadastro) |
| GET/PUT/DELETE | `/api/file-records/{id:guid}` | |
| POST | `/api/file-records` | |

Respostas de lista usam `PaginatedResponse<T>(Items, TotalCount, HasMore)`. A UI faz infinite scroll.

## Armadilhas

- **Busca é SQL cru** (`FileRecordService.BuildSearchWhereClause`): cada termo vira
  `unaccent(lower(col)) ILIKE '%termo%'` em `name OR client` (termo só de dígitos também compara igual com
  `file_number` e `flop_disk_number`), e os termos são combinados com AND. Além disso,
  a consulta inteira sem espaços e pontuação é comparada com `name`/`client` limpos do mesmo jeito
  (`regexp_replace(..., '[^[:alnum:]]', '', 'g')`), para "HORTODENTE" achar "Horto Dente". Depende da
  extensão `unaccent` (migration `AddUnaccentExtension`). Sempre use parâmetros Npgsql, nunca concatene
  entrada do usuário. Mudou o SQL? Rode `tests/Integration`; os testes de API usam InMemory e não o executam.
- **Lista de fichas (`FileRecords.razor.cs`):** toda operação que substitui a lista passa por
  `StartListRequest()` e descarta a resposta se `IsCurrentListRequest(version)` for falso. Mantenha esse
  padrão em novas cargas, senão respostas lentas sobrescrevem resultados novos. O termo da busca fica na URL
  (`/file-records?q=...`) e as páginas de edição recebem `?q=` para voltar à mesma busca.
- **`FileNumber` não é único:** é o número da caixa/lote e se repete em centenas de fichas (por isso o
  cadeado no cadastro). Duplicata, para o aviso do cadastro, é mesmo nome ignorando caixa, acento e espaços.
- **A página declara `lang="pt-BR" translate="no"`** (`App.razor`). Tradução automática do navegador quebra a
  renderização do Blazor (a tela mostra conteúdo antigo). Não remova.
- **Ordenação tem whitelist.** Para tornar uma coluna ordenável, atualize os três pontos juntos:
  `ValidSortFields`, o `switch` de `ApplySort` (LINQ) e confira se o nome bate com a coluna snake_case usada
  em `BuildOrderByClause` (SQL cru da busca). Padrão: `date DESC`; desempate sempre por `id`.
- **`FileType` é gravado como int.** Nunca reordene nem renumere o enum; só acrescente valores no fim.
- **Datas:** coluna `timestamptz`; o serviço força `DateTimeKind.Utc`. `Date` e `FileNumber` (`long`/bigint)
  são obrigatórios. O `.kiro/steering/product.md` ainda os descreve como opcionais/string (está desatualizado).
- **Migrations** só rodam automaticamente em `Production` (no startup da API). Em dev, aplique na mão.
  As migrations de importação do legado (`ImportGhlRegistros2_*`) leem SQL embutido de
  `src/Infrastructure/Migrations/Scripts/*.sql` via `EmbeddedResourceHelper`.
- **A API não sobe sem config do Firebase:** `Firebase:ProjectId` e `Firebase:ServiceAccountPath` (o arquivo
  precisa existir). A Web precisa de `Firebase:ApiKey`. Em dev isso vai em User Secrets
  (ver `docs/firebase-setup.md`).
- **Código morto aparente** em `FileRecordService`: `GetAllAsync`, `SearchAsync`, `ComputeScore`,
  `RemoveDiacritics` não são chamados por endpoints nem testes. Confirme antes de reaproveitar ou remover.

## Testes

- `tests/Api.Tests` usa **EF InMemory**, que não executa o SQL cru da busca nem `unaccent`. Busca e ordenação
  de verdade são testadas em `tests/Integration` (Testcontainers), que precisa de um runtime de containers.
- Property tests (FsCheck) seguem o nome dos specs: `Feature: <spec>, Property N: ...`.
- Nos testes de API, `CustomWebApplicationFactory` troca a auth por `TestAuthHandler` (sempre autenticado)
  e cria um banco InMemory por instância.
- **`FirebaseApp.DefaultInstance` é estado global do processo** e as classes de teste rodam em paralelo.
  Toda factory que pré-inicializa o Firebase deve envolver o `FirebaseApp.Create` em
  `try/catch (ArgumentException)`, porque outra factory pode criá-lo ao mesmo tempo. Sem isso, o construtor
  estático falha, a fixture não é criada e o FsCheck roda o teste com a factory nula (aparece como
  `NullReferenceException` no `CreateClient()`). `MissingConfigurationKeyPropertyTests` chega a apagar o app
  padrão durante a execução; se surgirem falhas intermitentes de Firebase nos testes, olhe ali primeiro.

## Ambiente local (Windows desta máquina)

Não há `make` nem `docker` no PATH; há **Podman** e o .NET SDK 10. Use os comandos `dotnet` direto:

```bash
podman compose up -d postgres          # Postgres 16 em localhost:5432 (sinterdb / sinterprints / sinterprints_dev)
dotnet build
dotnet test tests/Api.Tests
dotnet test tests/Web.Tests
dotnet test tests/Integration          # precisa do Podman rodando
dotnet ef database update --project src/Infrastructure --startup-project src/Api
dotnet ef migrations add <Nome> --project src/Infrastructure --startup-project src/Api
dotnet run --project src/Api           # http://localhost:5062
dotnet run --project src/Web           # http://localhost:5105 (ApiBaseUrl aponta para 5062)
```

Testcontainers com Podman pode exigir `DOCKER_HOST` apontando para o socket/pipe do Podman e
`TESTCONTAINERS_RYUK_DISABLED=true`.

## Deploy: push na `main` vai para produção

`.github/workflows/deploy.yml`: push na `main` → `dotnet test` → SCP do código para a VPS `pflausino.xyz`
(`/opt/apps/sinter-prints`) → `docker compose -f docker-compose.prod.yml build && up -d`. As migrations
pendentes rodam no startup da API em produção. Ou seja: **trabalhe em branch e abra PR; não faça push direto
na `main`** sem combinar antes. Detalhes em `deploy/README.md`.

## Fluxo de features

O projeto foi construído no fluxo de specs do Kiro: cada feature tem
`.kiro/specs/<feature>/{requirements,design,tasks}.md`. Antes de mexer numa feature existente, leia o spec
dela. Para features novas maiores, vale manter o mesmo formato.

## Roadmap (do product.md)

Preview/thumbnail dos arquivos, tags personalizáveis, versionamento de arquivos, busca por período de datas.
