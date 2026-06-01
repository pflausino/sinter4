# GHL registros2 import

The source dump is a MySQL-style `INSERT` file:

```sql
INSERT INTO u812598544_sinte.ghl_registros2
    (Nome,Firma,Tipo_de_Arquivo,N_do_Arquivo,Disquete,`Data`)
VALUES ...
```

Import it through a staging table, then normalize into `public.file_records`.

## Mapping

| Source column | Destination column | Notes |
| --- | --- | --- |
| `Nome` | `file_records.name` | Trimmed, stored as empty string if blank. |
| `Firma` | `file_records.client` | Trimmed, stored as empty string if blank or `NULL`. |
| `Tipo_de_Arquivo` | `file_records.file_type` | Mapped to the `FileType` enum. Unknown values become `Other`; blank values become `Unknown`. |
| `N_do_Arquivo` | `file_records.file_number` | Trimmed text. Values like `52B`, `PREF2`, and `DIVER` are preserved. |
| `Disquete` | `file_records.flop_disk_number` | Cast from numeric to integer. Source `0.0` is preserved as `0`; change the import SQL to `NULLIF(disquete::integer, 0)` if zero means unknown. |
| `Data` | `file_records.date` | Parsed as day/month/year. Supports `/` or `-`, one- or two-digit day/month, and two- or four-digit years. `NULL` or invalid dates stay `NULL`. |

## Run

Apply EF migrations first:

```bash
make migrate
```

Prepare the staging table:

```bash
psql "postgresql://sinterprints:sinterprints_dev@localhost:5432/sinterdb" \
  -f deploy/prepare-ghl-registros2-staging.sql
```

Load the dump into staging. The only required conversion is MySQL backticks around `Data`:

```bash
sed 's/`Data`/"Data"/g' docs/ghl_registros2_202605311436.sql |
  psql "postgresql://sinterprints:sinterprints_dev@localhost:5432/sinterdb"
```

Normalize into the app table:

```bash
psql "postgresql://sinterprints:sinterprints_dev@localhost:5432/sinterdb" \
  -f deploy/import-ghl-registros2.sql
```

The final script prints counts for staged rows, unparsed dates, `NULL` dates, and zero-valued disk rows.
