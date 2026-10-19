using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class CaseTreatmentsAndEncounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "information_source",
                table: "person_vaccines",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manufacturer",
                table: "person_vaccines",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "brief_note",
                table: "person_conditions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "expected_delivery_facility_id",
                table: "person_conditions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "occupation",
                table: "person_conditions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "pregnancy_due_date",
                table: "person_conditions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pregnant",
                table: "person_conditions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "encounters",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_condition_id = table.Column<int>(type: "int", nullable: false),
                    encounter_date = table.Column<DateOnly>(type: "date", nullable: false),
                    encounter_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    facility_id = table.Column<int>(type: "int", nullable: true),
                    description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounters", x => x.id);
                    table.ForeignKey(
                        name: "FK_encounters_facilities_facility_id",
                        column: x => x.facility_id,
                        principalTable: "facilities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_encounters_person_conditions_person_condition_id",
                        column: x => x.person_condition_id,
                        principalTable: "person_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_treatments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_condition_id = table.Column<int>(type: "int", nullable: false),
                    treatment_id = table.Column<int>(type: "int", nullable: false),
                    treatment_date = table.Column<DateOnly>(type: "date", nullable: true),
                    stop_treatment_date = table.Column<DateOnly>(type: "date", nullable: true),
                    dose = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    treatment_comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_treatments", x => x.id);
                    table.ForeignKey(
                        name: "FK_person_treatments_person_conditions_person_condition_id",
                        column: x => x.person_condition_id,
                        principalTable: "person_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_person_treatments_treatments_treatment_id",
                        column: x => x.treatment_id,
                        principalTable: "treatments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_person_conditions_expected_delivery_facility_id",
                table: "person_conditions",
                column: "expected_delivery_facility_id");

            migrationBuilder.CreateIndex(
                name: "IX_encounters_facility_id",
                table: "encounters",
                column: "facility_id");

            migrationBuilder.CreateIndex(
                name: "IX_encounters_person_condition_id",
                table: "encounters",
                column: "person_condition_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_treatments_person_condition_id",
                table: "person_treatments",
                column: "person_condition_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_treatments_treatment_id",
                table: "person_treatments",
                column: "treatment_id");

            migrationBuilder.AddForeignKey(
                name: "FK_person_conditions_facilities_expected_delivery_facility_id",
                table: "person_conditions",
                column: "expected_delivery_facility_id",
                principalTable: "facilities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_person_conditions_facilities_expected_delivery_facility_id",
                table: "person_conditions");

            migrationBuilder.DropTable(
                name: "encounters");

            migrationBuilder.DropTable(
                name: "person_treatments");

            migrationBuilder.DropIndex(
                name: "IX_person_conditions_expected_delivery_facility_id",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "information_source",
                table: "person_vaccines");

            migrationBuilder.DropColumn(
                name: "manufacturer",
                table: "person_vaccines");

            migrationBuilder.DropColumn(
                name: "brief_note",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "expected_delivery_facility_id",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "occupation",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "pregnancy_due_date",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "pregnant",
                table: "person_conditions");
        }
    }
}
