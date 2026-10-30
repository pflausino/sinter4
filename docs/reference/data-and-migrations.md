# Dados e Migrations

## Banco

- **PostgreSQL 16.** Local via `docker-compose.yml` (serviço `postgres`, banco `sinterdb`, usuário `sinterprints`, porta 5432).
- **EF Core 10 + Npgsql.** `AppDbContext` em `src/Infrastructure/Data/AppDbContext.cs`.
- **Schema padrão `public`.** Nomes em `snake_case` via `UseSnakeCaseNamingConvention()`.
- **Extensão `unaccent`**, necessária para a busca sem acentos.

## Convenções

- PKs `uuid`; datas `timestamptz`.
- Strings com limite explícito (`varchar(200)`) alinhado à validação dos DTOs.
- Sem stored procedures: a lógica fica em C#.
- Mudanças de dados com risco (backfill) ficam em migrations com SQL explícito e comentado.

## Histórico de migrations

Pasta: `src/Infrastructure/Migrations/`

| Migration | O que faz |
| --- | --- |
| `20260531160646_AddFileRecordsTable` | Cria `file_records` (id, name, file_type, flop_disk_number, date, client). |
| `20260531180257_AddFileNumberColumn` | Adiciona `file_number` (`text`, nulo). |
| `20260531211500_PrepareLegacyGhlImport` | Torna `date` nulável para aceitar dados legados sem data. |
| `20260602180520_AddUnaccentExtension` | `CREATE EXTENSION IF NOT EXISTS unaccent`. |
| `20260704131100_ImportGhlRegistros2_202607041311` | Importa o dump legado GHL a partir de script SQL embutido (`Migrations/Scripts/*.sql`, `EmbeddedResource`). Tem script `.down.sql` para rollback. |
| `20260705181641_AddFileRecordStringLengths` | `name`/`client` → `varchar(200)`; `file_number` → `varchar(50)`. |
| `20260706000000_BackfillNullDatesAndMakeDateNotNull` | Preenche `date` nula com o sentinela `1900-01-01` e torna a coluna `NOT NULL`. |
| `20260706010000_BackfillFileNumberAndConvertToInt` | `file_number` não numérico, nulo ou com mais de 11 dígitos → `'0'`; converte para `bigint NOT NULL DEFAULT 0`. |

```mermaid
timeline
    title Evolução de file_records
    31 mai : Tabela criada : file_number adicionado : date nulável (legado)
    02 jun : unaccent
    04 jul : Import GHL registros2
    05 jul : Limites de tamanho nas strings
    06 jul : date NOT NULL (sentinela 1900-01-01) : file_number bigint NOT NULL
```

> **Atenção:** as migrations de backfill são destrutivas para valores legados. Códigos como `DIVER` ou `246 CDR` em `file_number` viram `0`. Faça backup antes de aplicar em uma base com dados reais.

## Comandos

```bash
make migrate                       # aplica migrations (sobe o db antes)
make migration name=AddExample     # cria nova migration
# equivalente direto:
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

Em **produção**, a API aplica as migrations pendentes sozinha na inicialização (`IsProduction()`).

## Import legado GHL (`ghl_registros2`)

Os dados históricos vêm de um dump MySQL da tabela `ghl_registros2`. Há dois caminhos:

1. **Via migration** (`ImportGhlRegistros2_202607041311`): aplicado junto com `make migrate`/deploy.
2. **Manual via staging**: scripts em `deploy/` e passo a passo em [`docs/ghl-registros2-import.md`](../ghl-registros2-import.md).

Mapeamento resumido:

| Origem | Destino |
| --- | --- |
| `Nome` | `name` |
| `Firma` | `client` |
| `Tipo_de_Arquivo` | `file_type` (desconhecido → `Other`, vazio → `Unknown`) |
| `N_do_Arquivo` | `file_number` |
| `Disquete` | `flop_disk_number` |
| `Data` (dd/mm/aa ou dd/mm/aaaa) | `date` |

## Acesso local

```bash
psql "postgresql://sinterprints:sinterprints_dev@localhost:5432/sinterdb"
```

Credenciais apenas de desenvolvimento (`docker-compose.yml`).
