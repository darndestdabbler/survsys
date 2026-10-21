using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class VocabularyAtIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "agency_id",
                table: "system_messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "allow_new_cmr",
                table: "system_messages",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "allow_update_cmr",
                table: "system_messages",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "condition_id",
                table: "system_messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_surveillance",
                table: "system_messages",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "condition_id",
                table: "system_message_results",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "interpretation",
                table: "system_message_results",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "master_loinc",
                table: "system_message_results",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "organism_id",
                table: "system_message_results",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vocab_child_vocab",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lab_id = table.Column<int>(type: "int", nullable: false),
                    category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    child_code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    master_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocab_child_vocab", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vocab_master_vocab",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    concept = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    app_value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocab_master_vocab", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vocab_rule_conditions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vocab_rule_id = table.Column<int>(type: "int", nullable: false),
                    field = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "nvarchar(5)", maxLength: 5, nullable: false),
                    value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    and_or = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocab_rule_conditions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vocab_rules",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    condition_id = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    execution_order = table.Column<int>(type: "int", nullable: false),
                    allow_new_cmr = table.Column<bool>(type: "bit", nullable: false),
                    allow_update_cmr = table.Column<bool>(type: "bit", nullable: false),
                    is_surveillance = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocab_rules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vocab_child_vocab_lab_id_category_child_code",
                table: "vocab_child_vocab",
                columns: new[] { "lab_id", "category", "child_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vocab_master_vocab_category_concept",
                table: "vocab_master_vocab",
                columns: new[] { "category", "concept" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vocab_rule_conditions_vocab_rule_id",
                table: "vocab_rule_conditions",
                column: "vocab_rule_id");

            migrationBuilder.CreateIndex(
                name: "IX_vocab_rules_condition_id_kind",
                table: "vocab_rules",
                columns: new[] { "condition_id", "kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vocab_child_vocab");

            migrationBuilder.DropTable(
                name: "vocab_master_vocab");

            migrationBuilder.DropTable(
                name: "vocab_rule_conditions");

            migrationBuilder.DropTable(
                name: "vocab_rules");

            migrationBuilder.DropColumn(
                name: "agency_id",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "allow_new_cmr",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "allow_update_cmr",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "condition_id",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "is_surveillance",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "condition_id",
                table: "system_message_results");

            migrationBuilder.DropColumn(
                name: "interpretation",
                table: "system_message_results");

            migrationBuilder.DropColumn(
                name: "master_loinc",
                table: "system_message_results");

            migrationBuilder.DropColumn(
                name: "organism_id",
                table: "system_message_results");
        }
    }
}
