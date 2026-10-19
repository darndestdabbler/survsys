using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoreElements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "core_elements",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    uid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    tab = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    hidden = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_elements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "core_element_overrides",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    core_element_id = table.Column<int>(type: "int", nullable: false),
                    condition_id = table.Column<int>(type: "int", nullable: true),
                    agency_id = table.Column<int>(type: "int", nullable: true),
                    hidden = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_element_overrides", x => x.id);
                    table.CheckConstraint("ck_core_element_overrides_condition_or_agency", "condition_id IS NOT NULL OR agency_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_core_element_overrides_agencies_agency_id",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_element_overrides_conditions_condition_id",
                        column: x => x.condition_id,
                        principalTable: "conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_element_overrides_core_elements_core_element_id",
                        column: x => x.core_element_id,
                        principalTable: "core_elements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "core_elements",
                columns: new[] { "id", "hidden", "label", "tab", "uid" },
                values: new object[,]
                {
                    { 1, false, "Disease onset date", "Clinical", "disease_onset_date" },
                    { 2, false, "Date diagnosed", "Clinical", "date_diagnosed" },
                    { 3, false, "Treatment given", "Clinical", "treatment_given" },
                    { 4, false, "Died?", "Clinical", "person_died" },
                    { 5, false, "Date of death", "Clinical", "person_date_of_death" },
                    { 6, false, "Food handler", "Investigation", "food_handler" },
                    { 7, false, "Health care worker", "Investigation", "healthcare_worker" },
                    { 8, false, "Group living", "Investigation", "group_living" },
                    { 9, false, "Day care association", "Investigation", "day_care_association" },
                    { 10, false, "Imported from", "Investigation", "imported_from" },
                    { 11, false, "Transmission mode", "Investigation", "transmission_mode" },
                    { 12, false, "Risk factors", "Investigation", "risk_factors" },
                    { 13, false, "Risk factors notes", "Investigation", "risk_factors_notes" },
                    { 14, false, "Other data 1", "Investigation", "other_data_1" },
                    { 15, false, "Other data 2", "Investigation", "other_data_2" },
                    { 16, false, "LHD case status", "Administrative", "lhd_case_status" },
                    { 17, false, "State case status", "Administrative", "state_case_status" },
                    { 18, false, "Outbreak associated", "Administrative", "outbreak_associated" },
                    { 19, false, "LHD investigation started", "Administrative", "lhd_investigation_start_date" },
                    { 20, false, "LHD investigation completed", "Administrative", "lhd_investigation_completed_date" },
                    { 21, false, "Investigation outcome", "Administrative", "investigation_outcome" },
                    { 22, false, "Event name", "Administrative", "event_name" },
                    { 23, false, "Results reported to LHD", "Administrative", "results_reported_to_clinician_date" },
                    { 24, false, "Date CDC verbally notified", "Administrative", "cdc_first_verbally_notified_date" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_element_overrides_agency_id",
                table: "core_element_overrides",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "IX_core_element_overrides_condition_id",
                table: "core_element_overrides",
                column: "condition_id");

            migrationBuilder.CreateIndex(
                name: "IX_core_element_overrides_core_element_id_condition_id_agency_id",
                table: "core_element_overrides",
                columns: new[] { "core_element_id", "condition_id", "agency_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_elements_uid",
                table: "core_elements",
                column: "uid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "core_element_overrides");

            migrationBuilder.DropTable(
                name: "core_elements");
        }
    }
}
