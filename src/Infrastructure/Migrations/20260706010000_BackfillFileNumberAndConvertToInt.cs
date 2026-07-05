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
            // 1. Backfill: any file_number that is NULL or contains non-digit characters
            //    (letters, spaces, punctuation) is replaced by '0'. Legacy records with
            //    codes like 'DIVER', 'LOGOS', '246 CDR' collapse into the 0 sentinel.
            migrationBuilder.Sql(@"
                UPDATE public.file_records
                SET file_number = '0'
                WHERE file_number IS NULL
                   OR file_number !~ '^[0-9]+$';
            ");

            // 2. Convert the column type from character varying(50) to integer.
            //    The USING clause casts every (now-numeric) string value to int.
            migrationBuilder.Sql(@"
                ALTER TABLE public.file_records
                ALTER COLUMN file_number TYPE integer USING file_number::integer;
            ");

            // 3. Enforce NOT NULL (all rows have a value after step 1).
            migrationBuilder.AlterColumn<int>(
                name: "file_number",
                schema: "public",
                table: "file_records",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: relax NOT NULL and convert back to character varying(50).
            // The backfill cannot be undone because '0' is indistinguishable
            // from originally-'0' or originally-non-numeric rows once collapsed.
            migrationBuilder.AlterColumn<int>(
                name: "file_number",
                schema: "public",
                table: "file_records",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: false);

            migrationBuilder.Sql(@"
                ALTER TABLE public.file_records
                ALTER COLUMN file_number TYPE character varying(50) USING file_number::text;
            ");
        }
    }
}
