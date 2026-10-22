using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class TreatmentsAndVaccines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "treatments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    treatment_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    default_treatment = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_treatments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vaccines",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vaccines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "condition_treatments",
                columns: table => new
                {
                    condition_id = table.Column<int>(type: "int", nullable: false),
                    treatment_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_condition_treatments", x => new { x.condition_id, x.treatment_id });
                    table.ForeignKey(
                        name: "FK_condition_treatments_conditions_condition_id",
                        column: x => x.condition_id,
                        principalTable: "conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_condition_treatments_treatments_treatment_id",
                        column: x => x.treatment_id,
                        principalTable: "treatments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_vaccines",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_condition_id = table.Column<int>(type: "int", nullable: false),
                    vaccine_id = table.Column<int>(type: "int", nullable: false),
                    administered_date = table.Column<DateOnly>(type: "date", nullable: true),
                    dose_number = table.Column<int>(type: "int", nullable: true),
                    lot_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    expiration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_vaccines", x => x.id);
                    table.ForeignKey(
                        name: "FK_person_vaccines_person_conditions_person_condition_id",
                        column: x => x.person_condition_id,
                        principalTable: "person_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_person_vaccines_vaccines_vaccine_id",
                        column: x => x.vaccine_id,
                        principalTable: "vaccines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "vaccines",
                columns: new[] { "id", "code", "description", "sort_order" },
                values: new object[,]
                {
                    { 1, "20", "DTaP", 1 },
                    { 2, "106", "DTaP, 5 pertussis antigens", 2 },
                    { 3, "107", "DTaP, unspecified formulation", 3 },
                    { 4, "146", "DTaP-IPV-Hib-Hep B", 4 },
                    { 5, "110", "DTaP-Hep B-IPV", 5 },
                    { 6, "50", "DTaP-Hib", 6 },
                    { 7, "120", "DTaP-Hib-IPV", 7 },
                    { 8, "130", "DTaP-IPV", 8 },
                    { 9, "132", "DTaP-IPV-Hib-Hep B, historical", 9 },
                    { 10, "01", "DTP", 10 },
                    { 11, "22", "DTP-Hib", 11 },
                    { 12, "102", "DTP-Hib-Hep B", 12 },
                    { 13, "04", "M/R (Measles-Rubella)", 13 },
                    { 14, "05", "Measles ONLY", 14 },
                    { 15, "03", "MMR", 15 },
                    { 16, "94", "MMRV", 16 },
                    { 17, "07", "Mumps ONLY", 17 },
                    { 18, "998", "No Vaccine Administered", 18 },
                    { 19, "OTH", "Other", 19 },
                    { 20, "11", "Pertussis ONLY", 20 },
                    { 21, "06", "Rubella ONLY", 21 },
                    { 22, "38", "R/M (Rubella-Mumps)", 22 },
                    { 23, "115", "Tdap", 23 },
                    { 24, "999", "Unknown", 24 },
                    { 25, "21", "Varicella", 25 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_condition_treatments_treatment_id",
                table: "condition_treatments",
                column: "treatment_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_vaccines_person_condition_id",
                table: "person_vaccines",
                column: "person_condition_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_vaccines_vaccine_id",
                table: "person_vaccines",
                column: "vaccine_id");

            migrationBuilder.CreateIndex(
                name: "IX_vaccines_code",
                table: "vaccines",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "condition_treatments");

            migrationBuilder.DropTable(
                name: "person_vaccines");

            migrationBuilder.DropTable(
                name: "treatments");

            migrationBuilder.DropTable(
                name: "vaccines");
        }
    }
}
