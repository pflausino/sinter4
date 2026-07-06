using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillFileNumberAndConvertToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Backfill: any file_number that is NULL, contains non-digit characters
            //    (letters, spaces, punctuation), or has more than 11 digits is replaced
            //    by '0'. Legacy codes like 'DIVER', 'LOGOS', '246 CDR' collapse into the
            //    0 sentinel. The 11-digit cap enforces the business limit and guarantees
            //    every remaining value fits in bigint, so the cast in step 2 can't overflow.
            migrationBuilder.Sql(@"
                UPDATE public.file_records
                SET file_number = '0'
                WHERE file_number IS NULL
                   OR file_number !~ '^[0-9]+$'
                   OR length(file_number) > 11;
            ");

            // 2. Convert the column type from character varying(50) to bigint.
            //    The USING clause casts every (now-numeric, <= 11 digit) string value.
            migrationBuilder.Sql(@"
                ALTER TABLE public.file_records
                ALTER COLUMN file_number TYPE bigint USING file_number::bigint;
            ");

            // 3. Enforce NOT NULL (all rows have a value after step 1).
            migrationBuilder.AlterColumn<long>(
                name: "file_number",
                schema: "public",
                table: "file_records",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: relax NOT NULL and convert back to character varying(50).
            // The backfill cannot be undone because '0' is indistinguishable
            // from originally-'0' or originally-non-numeric rows once collapsed.
            migrationBuilder.AlterColumn<long>(
                name: "file_number",
                schema: "public",
                table: "file_records",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: false);

            migrationBuilder.Sql(@"
                ALTER TABLE public.file_records
                ALTER COLUMN file_number TYPE character varying(50) USING file_number::text;
            ");
        }
    }
}
