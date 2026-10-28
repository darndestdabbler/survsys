using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class EcrValidationFindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ecr_validation_finding",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ecr_document_id = table.Column<int>(type: "int", nullable: false),
                    source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    severity = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    location = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ecr_validation_finding", x => x.id);
                    table.ForeignKey(
                        name: "FK_ecr_validation_finding_ecr_document_ecr_document_id",
                        column: x => x.ecr_document_id,
                        principalTable: "ecr_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ecr_validation_finding_ecr_document_id",
                table: "ecr_validation_finding",
                column: "ecr_document_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ecr_validation_finding");
        }
    }
}
