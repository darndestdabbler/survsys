using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalAndLaboratory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_death",
                table: "persons",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "death_status",
                table: "persons",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "date_diagnosed",
                table: "person_conditions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "disease_onset_date",
                table: "person_conditions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "treatment_given",
                table: "person_conditions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "labs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    lab_facility_id = table.Column<int>(type: "int", nullable: true),
                    provider_id = table.Column<int>(type: "int", nullable: true),
                    diagnostic_facility_id = table.Column<int>(type: "int", nullable: true),
                    reporting_facility_id = table.Column<int>(type: "int", nullable: true),
                    specimen_source_id = table.Column<int>(type: "int", nullable: true),
                    collection_date = table.Column<DateOnly>(type: "date", nullable: true),
                    accession_no = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    specimen_sent_to_state = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_labs", x => x.id);
                    table.ForeignKey(
                        name: "FK_labs_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lab_resist_tests",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lab_id = table.Column<int>(type: "int", nullable: false),
                    organism_id = table.Column<int>(type: "int", nullable: true),
                    resist_test_agent_id = table.Column<int>(type: "int", nullable: true),
                    resist_test_result_id = table.Column<int>(type: "int", nullable: true),
                    test_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lab_resist_tests", x => x.id);
                    table.ForeignKey(
                        name: "FK_lab_resist_tests_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lab_tests",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lab_id = table.Column<int>(type: "int", nullable: false),
                    test_type_id = table.Column<int>(type: "int", nullable: true),
                    test_status_id = table.Column<int>(type: "int", nullable: true),
                    loinc_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    reference_range = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    lab_test_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lab_tests", x => x.id);
                    table.ForeignKey(
                        name: "FK_lab_tests_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_condition_labs",
                columns: table => new
                {
                    person_condition_id = table.Column<int>(type: "int", nullable: false),
                    lab_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_condition_labs", x => new { x.person_condition_id, x.lab_id });
                    table.ForeignKey(
                        name: "FK_person_condition_labs_labs_lab_id",
                        column: x => x.lab_id,
                        principalTable: "labs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_person_condition_labs_person_conditions_person_condition_id",
                        column: x => x.person_condition_id,
                        principalTable: "person_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lab_test_results",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lab_test_id = table.Column<int>(type: "int", nullable: false),
                    organism_id = table.Column<int>(type: "int", nullable: true),
                    result_value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    test_result = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    units = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    primary_lab_result = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lab_test_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_lab_test_results_lab_tests_lab_test_id",
                        column: x => x.lab_test_id,
                        principalTable: "lab_tests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lab_resist_tests_lab_id",
                table: "lab_resist_tests",
                column: "lab_id");

            migrationBuilder.CreateIndex(
                name: "IX_lab_test_results_lab_test_id",
                table: "lab_test_results",
                column: "lab_test_id");

            migrationBuilder.CreateIndex(
                name: "IX_lab_tests_lab_id",
                table: "lab_tests",
                column: "lab_id");

            migrationBuilder.CreateIndex(
                name: "IX_labs_person_id",
                table: "labs",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_condition_labs_lab_id",
                table: "person_condition_labs",
                column: "lab_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lab_resist_tests");

            migrationBuilder.DropTable(
                name: "lab_test_results");

            migrationBuilder.DropTable(
                name: "person_condition_labs");

            migrationBuilder.DropTable(
                name: "lab_tests");

            migrationBuilder.DropTable(
                name: "labs");

            migrationBuilder.DropColumn(
                name: "date_of_death",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "death_status",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "date_diagnosed",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "disease_onset_date",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "treatment_given",
                table: "person_conditions");
        }
    }
}
