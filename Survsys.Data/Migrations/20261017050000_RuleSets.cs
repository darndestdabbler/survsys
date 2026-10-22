using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class RuleSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rule_set",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_set", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rule_set_config",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    format_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    value_set_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    repeating = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_set_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rule_set_data",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    rule_set_config_id = table.Column<int>(type: "int", nullable: false),
                    data_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    data_value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_set_data", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rule_set_expression",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    rule_set_config_id = table.Column<int>(type: "int", nullable: false),
                    value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    other_specified = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    execution_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_set_expression", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rule_set_expression_condition",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    rule_set_expression_id = table.Column<int>(type: "int", nullable: false),
                    field = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    @operator = table.Column<string>(name: "operator", type: "nvarchar(10)", maxLength: 10, nullable: true),
                    value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    and_or = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    value_is_field = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_set_expression_condition", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rule_set_field",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    rule_set_config_id = table.Column<int>(type: "int", nullable: false),
                    field = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_set_field", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rule_set_value_map",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    rule_set_config_id = table.Column<int>(type: "int", nullable: false),
                    value_from = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    value_to = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    other_specified = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_set_value_map", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "rule_set",
                columns: new[] { "id", "label" },
                values: new object[,]
                {
                    { 1, "staticValue" },
                    { 2, "cdcFirstSent" },
                    { 3, "cdcDiseaseId" },
                    { 4, "fieldValueMap" },
                    { 5, "conditionMap" },
                    { 6, "notificationStatusResponse" },
                    { 7, "repeatingGroup" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rule_set");

            migrationBuilder.DropTable(
                name: "rule_set_config");

            migrationBuilder.DropTable(
                name: "rule_set_data");

            migrationBuilder.DropTable(
                name: "rule_set_expression");

            migrationBuilder.DropTable(
                name: "rule_set_expression_condition");

            migrationBuilder.DropTable(
                name: "rule_set_field");

            migrationBuilder.DropTable(
                name: "rule_set_value_map");
        }
    }
}
