using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillNullDatesAndMakeDateNotNull : Migration
    {
        /// <summary>
        /// Sentinel value assigned to records whose date was NULL. Chosen as a clearly
        /// historical, human-readable placeholder that is valid in every timezone/system.
        /// </summary>
        private const string MinimumDateLiteral = "1900-01-01 00:00:00+00";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Backfill: replace all NULL dates with the sentinel minimum.
            migrationBuilder.Sql($@"
                UPDATE public.file_records
                SET date = TIMESTAMPTZ '{MinimumDateLiteral}'
                WHERE date IS NULL;
            ");

            // 2. Schema change: make date NOT NULL going forward.
            migrationBuilder.AlterColumn<DateTime>(
                name: "date",
                schema: "public",
                table: "file_records",
                type: "timestamptz",
                nullable: false,
                defaultValue: new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                oldClrType: typeof(DateTime),
                oldType: "timestamptz",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the schema change only. The backfill cannot be reversed because
            // once records share the sentinel value we can no longer distinguish
            // originally-NULL rows from ones that were legitimately dated 1900-01-01.
            migrationBuilder.AlterColumn<DateTime>(
                name: "date",
                schema: "public",
                table: "file_records",
                type: "timestamptz",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamptz",
                oldNullable: false);
        }
    }
}
