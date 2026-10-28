using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class EcrDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ecr_document",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    document_id = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    feed = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    set_id = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    version_number = table.Column<int>(type: "int", nullable: true),
                    eicr_document_id = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    raw_xml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    source = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    received_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ecr_document", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ecr_document_document_id",
                table: "ecr_document",
                column: "document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ecr_document_eicr_document_id",
                table: "ecr_document",
                column: "eicr_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_ecr_document_set_id",
                table: "ecr_document",
                column: "set_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ecr_document");
        }
    }
}
