using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotificationGuidesAndPhinVariables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "condition_detail",
                columns: table => new
                {
                    condition_id = table.Column<int>(type: "int", nullable: false),
                    guide_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_condition_detail", x => x.condition_id);
                });

            migrationBuilder.CreateTable(
                name: "guide",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "guide_link",
                columns: table => new
                {
                    guide_id = table.Column<int>(type: "int", nullable: false),
                    linked_guide_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_link", x => new { x.guide_id, x.linked_guide_id });
                });

            migrationBuilder.CreateTable(
                name: "guide_phin_var",
                columns: table => new
                {
                    guide_id = table.Column<int>(type: "int", nullable: false),
                    phin_var = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_phin_var", x => new { x.guide_id, x.phin_var });
                });

            migrationBuilder.CreateTable(
                name: "phin_var",
                columns: table => new
                {
                    phin_var = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    identifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    priority = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    hl7_data_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    rule_set_id = table.Column<int>(type: "int", nullable: true),
                    rule_set_config_id = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    code_system = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_phin_var", x => x.phin_var);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "condition_detail");

            migrationBuilder.DropTable(
                name: "guide");

            migrationBuilder.DropTable(
                name: "guide_link");

            migrationBuilder.DropTable(
                name: "guide_phin_var");

            migrationBuilder.DropTable(
                name: "phin_var");
        }
    }
}
