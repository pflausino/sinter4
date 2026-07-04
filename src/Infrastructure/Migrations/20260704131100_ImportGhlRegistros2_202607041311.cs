using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImportGhlRegistros2_202607041311 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sql = EmbeddedResourceHelper.ReadResource(
                "Infrastructure.Migrations.Scripts.import-ghl-registros2-202607041311.sql");
            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove all records inserted by this import based on file_number values used
            migrationBuilder.Sql("""
                DELETE FROM public.file_records
                WHERE file_number IN ('2040', '2041')
                  AND date = TIMESTAMPTZ '2026-06-17 00:00:00+00';
                """);
        }
    }
}
