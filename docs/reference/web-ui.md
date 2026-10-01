# Frontend Web (Blazor Server)

Projeto: `src/Web`. Blazor Server com render mode `InteractiveServer` nas páginas interativas. O Web **não acessa o banco**: todos os dados vêm da API via `AuthenticatedHttpClient`.

## Rotas

| Rota | Componente | Auth | Descrição |
| --- | --- | --- | --- |
| `/`, `/login` | `Pages/Login.razor` | Não | Login e-mail/senha. Usa `LoginLayout` (sem menu). |
| `/dashboard` | `Pages/Dashboard.razor` | Sim | Boas-vindas. |
| `/file-records` | `Pages/FileRecords.razor` | Sim | Listagem, busca, ordenação, colunas, exclusão. |
| `/file-records/new` | `Pages/FileRecordCreate.razor` | Sim | Cadastro de ficha. |
| `/file-records/{id}/edit` | `Pages/FileRecordEdit.razor` | Sim | Edição de ficha. |
| `/health` | `Pages/Health.razor` | Sim | Mostra o `/health` da API. |
| (outras) | `Pages/NotFound.razor` | — | Página 404. |

O roteamento (`Routes.razor`) usa `AuthorizeRouteView`. Rotas protegidas sem usuário renderizam `RedirectToLogin`. `BlazorAuthorizationMiddlewareResultHandler` impede que o middleware do servidor faça challenge/redirect, e quem decide é o componente.

## Layout

- `MainLayout`: sidebar com `NavMenu` (Home, Arquivos, Health, Sair) e área de conteúdo.
- `LoginLayout`: layout vazio para a tela de login.
- `ReconnectModal`: modal de reconexão do circuito Blazor.

## Página `FileRecords` em detalhe

Arquivos: `FileRecords.razor`, `FileRecords.razor.cs`, `FileRecords.razor.css`.

| Recurso | Implementação |
| --- | --- |
| Paginação | `PageSize = 50`. Rolagem infinita via `IntersectionObserver` (`wwwroot/js/infinite-scroll.js`, `rootMargin: 200px`) chamando `[JSInvokable] LoadMoreItems`. |
| Busca | Enter ou botão. Chama `/api/file-records/search` com `offset`/`limit` e a ordenação atual. Mostra estados de vazio, erro e contagem. |
| Ordenação | Clique/Enter/Espaço no `<th>` (com `aria-sort`); no mobile, `<select>` + botão de direção. Padrão inicial: `file_number desc`. |
| Colunas | Nome, Tipo, Nº Arquivo, Cliente, Data, Nº Disquete (oculta por padrão), Ações. Pelo menos uma sempre visível. Dropdown fecha com clique fora (`wwwroot/js/columnSelector.js`) ou Esc. |
| Responsivo | Tabela no desktop e cards no mobile. |
| Exclusão | Modal de confirmação com estado "Excluindo...". |

## Formulários (`FileRecordCreate` / `FileRecordEdit`)

- `EditForm` + `DataAnnotationsValidator` usando os DTOs de `Shared`. As mesmas regras valem na API.
- Navegação por **Enter** entre campos (`wwwroot/js/form-navigation.js`). No último campo, Enter submete.
- **Cadeado do Nº Arquivo** (só no cadastro): mantém o número após salvar para cadastros em série.

## Serviços

| Serviço | Lifetime | Função |
| --- | --- | --- |
| `FirebaseTokenProvider` (`ITokenProvider`) | Scoped | Login (`signInWithPassword`), refresh (`securetoken`), sign-out, leitura do token. Traduz erros do Firebase para pt-BR. |
| `FirebaseAuthStateProvider` (`AuthenticationStateProvider`) | Scoped | Lê o JWT do storage, extrai claims, renova se faltar ≤ 5 min para expirar e redireciona ao login se falhar. |
| `ProtectedTokenStorage` (`ITokenStorage`) | Scoped | Wrapper de `ProtectedLocalStorage`. |
| `AuthenticatedHttpClient` | Scoped | Cria o `HttpClient "Api"` com `Authorization: Bearer`. |

HttpClients nomeados: `Api` (base `ApiBaseUrl`) e `FirebaseAuth` (timeout de 15 s).

Detalhes do ciclo de autenticação: [Autenticação e Segurança](auth-and-security.md).

## Configuração

| Chave | Onde | Descrição |
| --- | --- | --- |
| `ApiBaseUrl` | appsettings / env | URL da API (dev: `http://localhost:5062`). |
| `Firebase:ApiKey` | User Secrets / env | Web API key do Firebase. |
| `DataProtection:KeyPath` | env (prod) | Diretório onde as chaves do Data Protection são persistidas (`/keys`). |
