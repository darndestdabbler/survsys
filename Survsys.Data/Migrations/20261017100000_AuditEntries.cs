using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    table_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    key_values = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    operation = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_table_name_occurred_at",
                table: "audit_entries",
                columns: new[] { "table_name", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entries");
        }
    }
}
