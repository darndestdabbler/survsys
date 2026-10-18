using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class VocabularyMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vocab_child_loinc",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    child_loinc = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    master_loinc_id = table.Column<int>(type: "int", nullable: false),
                    lab_id = table.Column<int>(type: "int", nullable: false),
                    concept_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    archived = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocab_child_loinc", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vocab_master_loinc",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    loinc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    concept_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    condition_id = table.Column<int>(type: "int", nullable: true),
                    organism_id = table.Column<int>(type: "int", nullable: true),
                    organism_from_result = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocab_master_loinc", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vocab_master_organism",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    snomed = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    snomed_alt = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    organism = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    condition_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocab_master_organism", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vocab_child_loinc_lab_id_child_loinc",
                table: "vocab_child_loinc",
                columns: new[] { "lab_id", "child_loinc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vocab_master_loinc_loinc",
                table: "vocab_master_loinc",
                column: "loinc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vocab_master_organism_snomed",
                table: "vocab_master_organism",
                column: "snomed",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vocab_child_loinc");

            migrationBuilder.DropTable(
                name: "vocab_master_loinc");

            migrationBuilder.DropTable(
                name: "vocab_master_organism");
        }
    }
}
