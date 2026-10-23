using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class EcrReportability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ecr_condition_mapping",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code_system = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    condition_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ecr_condition_mapping", x => x.id);
                    table.ForeignKey(
                        name: "FK_ecr_condition_mapping_conditions_condition_id",
                        column: x => x.condition_id,
                        principalTable: "conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ecr_rr_condition",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ecr_document_id = table.Column<int>(type: "int", nullable: false),
                    code_system = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    display_name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    determination_code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    responsible_agency_id = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    reporting_timeframe = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ecr_rr_condition", x => x.id);
                    table.ForeignKey(
                        name: "FK_ecr_rr_condition_ecr_document_ecr_document_id",
                        column: x => x.ecr_document_id,
                        principalTable: "ecr_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ecr_condition_mapping_code_system_code",
                table: "ecr_condition_mapping",
                columns: new[] { "code_system", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ecr_condition_mapping_condition_id",
                table: "ecr_condition_mapping",
                column: "condition_id");

            migrationBuilder.CreateIndex(
                name: "IX_ecr_rr_condition_ecr_document_id",
                table: "ecr_rr_condition",
                column: "ecr_document_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ecr_condition_mapping");

            migrationBuilder.DropTable(
                name: "ecr_rr_condition");
        }
    }
}
