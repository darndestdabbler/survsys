using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class Outbreaks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "outbreak_id",
                table: "person_conditions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "outbreaks",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    outbreak_date = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    condition_id = table.Column<int>(type: "int", nullable: true),
                    agency_id = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbreaks", x => x.id);
                    table.ForeignKey(
                        name: "FK_outbreaks_agencies_agency_id",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_outbreaks_conditions_condition_id",
                        column: x => x.condition_id,
                        principalTable: "conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_sections",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_sections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbreak_access",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    outbreak_id = table.Column<int>(type: "int", nullable: false),
                    agency_id = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbreak_access", x => x.id);
                    table.ForeignKey(
                        name: "FK_outbreak_access_agencies_agency_id",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_outbreak_access_outbreaks_outbreak_id",
                        column: x => x.outbreak_id,
                        principalTable: "outbreaks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outbreak_updates",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    outbreak_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbreak_updates", x => x.id);
                    table.ForeignKey(
                        name: "FK_outbreak_updates_outbreaks_outbreak_id",
                        column: x => x.outbreak_id,
                        principalTable: "outbreaks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_outbreak_updates_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outbreak_report_sections",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    outbreak_id = table.Column<int>(type: "int", nullable: false),
                    report_section_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    report_text = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbreak_report_sections", x => x.id);
                    table.ForeignKey(
                        name: "FK_outbreak_report_sections_outbreaks_outbreak_id",
                        column: x => x.outbreak_id,
                        principalTable: "outbreaks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_outbreak_report_sections_report_sections_report_section_id",
                        column: x => x.report_section_id,
                        principalTable: "report_sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_outbreak_report_sections_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "report_sections",
                columns: new[] { "id", "description" },
                values: new object[,]
                {
                    { 1, "Context/Background" },
                    { 2, "Initiation of Investigation" },
                    { 3, "Investigation Methods" },
                    { 4, "Investigation Findings/Results" },
                    { 5, "Discussion and/or Conclusions" },
                    { 6, "Recommendations for Controlling Disease and/or Preventing/Mitigating Exposure" },
                    { 7, "Key Investigators and/or Report Authors" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_person_conditions_outbreak_id",
                table: "person_conditions",
                column: "outbreak_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbreak_access_agency_id",
                table: "outbreak_access",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbreak_access_outbreak_id_agency_id",
                table: "outbreak_access",
                columns: new[] { "outbreak_id", "agency_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbreak_report_sections_outbreak_id_report_section_id",
                table: "outbreak_report_sections",
                columns: new[] { "outbreak_id", "report_section_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbreak_report_sections_report_section_id",
                table: "outbreak_report_sections",
                column: "report_section_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbreak_report_sections_user_id",
                table: "outbreak_report_sections",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbreak_updates_outbreak_id_created_at",
                table: "outbreak_updates",
                columns: new[] { "outbreak_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_outbreak_updates_user_id",
                table: "outbreak_updates",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbreaks_agency_id",
                table: "outbreaks",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbreaks_condition_id",
                table: "outbreaks",
                column: "condition_id");

            migrationBuilder.AddForeignKey(
                name: "FK_person_conditions_outbreaks_outbreak_id",
                table: "person_conditions",
                column: "outbreak_id",
                principalTable: "outbreaks",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_person_conditions_outbreaks_outbreak_id",
                table: "person_conditions");

            migrationBuilder.DropTable(
                name: "outbreak_access");

            migrationBuilder.DropTable(
                name: "outbreak_report_sections");

            migrationBuilder.DropTable(
                name: "outbreak_updates");

            migrationBuilder.DropTable(
                name: "report_sections");

            migrationBuilder.DropTable(
                name: "outbreaks");

            migrationBuilder.DropIndex(
                name: "IX_person_conditions_outbreak_id",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "outbreak_id",
                table: "person_conditions");
        }
    }
}
