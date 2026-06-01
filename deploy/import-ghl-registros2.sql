BEGIN;

CREATE OR REPLACE FUNCTION pg_temp.legacy_ghl_uuid(source_key text)
RETURNS uuid
LANGUAGE sql
IMMUTABLE
STRICT
AS $$
    WITH hash AS (
        SELECT md5(source_key) AS value
    )
    SELECT (
        substr(value, 1, 8) || '-' ||
        substr(value, 9, 4) || '-' ||
        substr(value, 13, 4) || '-' ||
        substr(value, 17, 4) || '-' ||
        substr(value, 21, 12)
    )::uuid
    FROM hash;
$$;

CREATE OR REPLACE FUNCTION pg_temp.legacy_ghl_file_type(raw_value text)
RETURNS integer
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT CASE lower(btrim(coalesce(raw_value, '')))
        WHEN 'corel' THEN 0
        WHEN 'coreldraw' THEN 0
        WHEN 'photoshop' THEN 1
        WHEN 'illustrator' THEN 2
        WHEN 'pdf' THEN 4
        WHEN 'indesign' THEN 6
        WHEN 'indd' THEN 6
        WHEN 'pagemaker' THEN 7
        WHEN 'pagmaker' THEN 7
        WHEN 'pmd' THEN 7
        WHEN 'jpeg' THEN 8
        WHEN 'jpg' THEN 8
        WHEN 'png' THEN 9
        WHEN 'tif' THEN 10
        WHEN 'tiff' THEN 10
        WHEN 'eps' THEN 11
        WHEN 'old' THEN 12
        WHEN '' THEN 13
        ELSE 5
    END;
$$;

CREATE OR REPLACE FUNCTION pg_temp.legacy_ghl_parse_date(raw_value text)
RETURNS timestamp with time zone
LANGUAGE plpgsql
IMMUTABLE
AS $$
DECLARE
    normalized text;
    parts text[];
    day_part integer;
    month_part integer;
    year_part integer;
    parsed_date date;
BEGIN
    IF raw_value IS NULL THEN
        RETURN NULL;
    END IF;

    normalized := regexp_replace(btrim(raw_value), '\s+', '', 'g');
    IF normalized = '' THEN
        RETURN NULL;
    END IF;

    parts := regexp_match(normalized, '^([0-9]{1,2})[/-]([0-9]{1,2})[/-]([0-9]{2}|[0-9]{4})$');
    IF parts IS NULL THEN
        RETURN NULL;
    END IF;

    day_part := parts[1]::integer;
    month_part := parts[2]::integer;
    year_part := parts[3]::integer;

    IF year_part < 100 THEN
        year_part := CASE
            WHEN year_part >= 70 THEN 1900 + year_part
            ELSE 2000 + year_part
        END;
    END IF;

    BEGIN
        parsed_date := make_date(year_part, month_part, day_part);
    EXCEPTION WHEN others THEN
        RETURN NULL;
    END;

    RETURN parsed_date::timestamp AT TIME ZONE 'UTC';
END;
$$;

INSERT INTO public.file_records
(
    id,
    name,
    file_type,
    flop_disk_number,
    date,
    client,
    file_number
)
SELECT
    pg_temp.legacy_ghl_uuid('ghl_registros2_202605311436:' || import_id::text),
    coalesce(nullif(btrim(nome), ''), ''),
    pg_temp.legacy_ghl_file_type(tipo_de_arquivo),
    CASE
        WHEN disquete IS NULL THEN NULL
        ELSE disquete::integer
    END,
    pg_temp.legacy_ghl_parse_date("Data"),
    coalesce(nullif(btrim(firma), ''), ''),
    nullif(btrim(coalesce(n_do_arquivo, '')), '')
FROM u812598544_sinte.ghl_registros2
ORDER BY import_id
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    file_type = EXCLUDED.file_type,
    flop_disk_number = EXCLUDED.flop_disk_number,
    date = EXCLUDED.date,
    client = EXCLUDED.client,
    file_number = EXCLUDED.file_number;

SELECT
    count(*) AS staged_rows,
    count(*) FILTER (
        WHERE "Data" IS NOT NULL
          AND btrim("Data") <> ''
          AND pg_temp.legacy_ghl_parse_date("Data") IS NULL
    ) AS unparsed_date_rows,
    count(*) FILTER (WHERE "Data" IS NULL) AS null_date_rows,
    count(*) FILTER (WHERE disquete = 0) AS zero_disk_rows
FROM u812598544_sinte.ghl_registros2;

COMMIT;
