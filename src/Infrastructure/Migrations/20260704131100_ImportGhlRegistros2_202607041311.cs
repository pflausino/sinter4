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
            // Surgically remove only the rows this import inserted, matched by primary key,
            // so records added later that happen to share the same file_number/date are preserved.
            var sql = EmbeddedResourceHelper.ReadResource(
                "Infrastructure.Migrations.Scripts.import-ghl-registros2-202607041311.down.sql");
            migrationBuilder.Sql(sql);
        }
    }
}
