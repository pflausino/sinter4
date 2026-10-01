# Referência da API

API REST em ASP.NET Core Minimal APIs (`src/Api`). JSON em camelCase.

- **Base URL local:** `http://localhost:5062` (perfil `http` em `launchSettings.json`, usado pelo Web em dev)
- **Autenticação:** `Authorization: Bearer <Firebase ID token>`, exigida em tudo, exceto `/health`.

## Resumo dos endpoints

| Método | Rota | Auth | Descrição |
| --- | --- | --- | --- |
| `GET` | `/health` | Não | Status da API e da conexão com o banco. |
| `GET` | `/api/me` | Sim | `uid` e `email` do usuário autenticado. |
| `GET` | `/api/file-records` | Sim | Lista paginada e ordenada. |
| `GET` | `/api/file-records/search` | Sim | Busca paginada por nome/cliente. |
| `GET` | `/api/file-records/{id}` | Sim | Detalhe de uma ficha. |
| `POST` | `/api/file-records` | Sim | Cria ficha. |
| `PUT` | `/api/file-records/{id}` | Sim | Atualiza ficha. |
| `DELETE` | `/api/file-records/{id}` | Sim | Remove ficha. |

Definições: `src/Api/Endpoints/FileRecordEndpoints.cs`, `src/Api/Endpoints/HealthEndpoints.cs`, `src/Api/Program.cs`.

## Parâmetros comuns de paginação e ordenação

| Parâmetro | Tipo | Default | Regras |
| --- | --- | --- | --- |
| `offset` | int | `0` | Valores negativos viram `0`. |
| `limit` | int | `50` | Limitado entre `1` e `100`. |
| `sortBy` | string | `date` | Whitelist: `name`, `file_type`, `file_number`, `client`, `date`, `flop_disk_number` (case-insensitive). Valor inválido ou ausente força `date DESC` e ignora `sortDir`. |
| `sortDir` | string | `asc` (`desc` para `date`) | `asc` ou `desc`. |

Ordem de nulos (`flop_disk_number`): **ASC → nulos por último**, **DESC → nulos primeiro**. O desempate é sempre por `id`.

### Resposta paginada

```json
{
  "items": [ /* FileRecordResponse */ ],
  "totalCount": 1234,
  "hasMore": true
}
```

`hasMore = offset + items.length < totalCount`.

## Endpoints

### `GET /health`

Sem autenticação. Sempre responde `200`; o estado vai no corpo.

```json
{ "status": "Healthy", "timestamp": "2026-10-01T12:00:00Z" }
```

`status` é `Healthy` ou `Unhealthy`, conforme `Database.CanConnectAsync()`.

### `GET /api/me`

```json
{ "uid": "firebase-uid", "email": "usuario@exemplo.com" }
```

### `GET /api/file-records`

Lista todas as fichas com paginação e ordenação.

```bash
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5062/api/file-records?offset=0&limit=50&sortBy=file_number&sortDir=desc"
```

### `GET /api/file-records/search`

| Parâmetro | Regras |
| --- | --- |
| `q` | Termo de busca. Vazio ou só espaços → `200` com lista vazia. Mais de 200 caracteres → `400`. |
| `offset`, `limit`, `sortBy`, `sortDir` | Iguais à listagem. |

**Semântica da busca:**

- `q` é dividido por espaços em termos.
- Cada termo precisa casar (`AND`) com `name` **ou** `client`.
- O casamento é `unaccent(lower(coluna)) ILIKE unaccent(lower('%termo%'))`: ignora acentos e maiúsculas e encontra substrings.

```bash
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5062/api/file-records/search?q=logo%20padaria&limit=20"
```

Erros: `400 { "error": "Search term must not exceed 200 characters" }` · `500` em erro de banco (logado).

### `GET /api/file-records/{id}`

`200 FileRecordResponse` ou `404`.

### `POST /api/file-records`

```json
{
  "name": "Logo Empresa XYZ",
  "fileType": 0,
  "flopDiskNumber": 12,
  "date": "2024-03-10",
  "client": "Empresa XYZ",
  "fileNumber": 1179
}
```

`201 Created` com header `Location: /api/file-records/{id}` e o corpo criado. `name` e `client` passam por trim; `date` é gravada como UTC.

### `PUT /api/file-records/{id}`

Mesmo corpo do POST. `200` com a ficha atualizada ou `404`.

### `DELETE /api/file-records/{id}`

`204 No Content` ou `404`.

## Erros

| Status | Quando | Corpo |
| --- | --- | --- |
| `400` | Falha de validação (POST/PUT) | `{ "errors": ["O nome é obrigatório.", "..."] }` |
| `400` | Corpo ausente | `{ "errors": ["Request body is required."] }` |
| `400` | `q` com mais de 200 caracteres | `{ "error": "Search term must not exceed 200 characters" }` |
| `401` | Sem header `Authorization` | `{ "error": "missing_token", "message": "No authentication token was provided." }` |
| `401` | Token inválido | `{ "error": "invalid_token", "message": "The token is invalid." }` |
| `401` | Token expirado | `{ "error": "token_expired", "message": "The token has expired." }` |
| `404` | Ficha não encontrada | — |
| `500` | Erro de banco na busca | — |

Regras de validação por campo: [Modelo de Domínio](domain-model.md#validação-dtos-de-entrada).
