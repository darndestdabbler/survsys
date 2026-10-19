using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersonConditions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "person_conditions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    condition_id = table.Column<int>(type: "int", nullable: false),
                    record_number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    agency_id = table.Column<int>(type: "int", nullable: true),
                    first_reported_ph_date = table.Column<DateOnly>(type: "date", nullable: true),
                    event_onset_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_conditions", x => x.id);
                    table.ForeignKey(
                        name: "FK_person_conditions_conditions_condition_id",
                        column: x => x.condition_id,
                        principalTable: "conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_person_conditions_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_persons_last_name_first_name",
                table: "persons",
                columns: new[] { "last_name", "first_name" });

            migrationBuilder.CreateIndex(
                name: "IX_person_conditions_condition_id",
                table: "person_conditions",
                column: "condition_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_conditions_person_id",
                table: "person_conditions",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_conditions_record_number",
                table: "person_conditions",
                column: "record_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "person_conditions");

            migrationBuilder.DropIndex(
                name: "IX_persons_last_name_first_name",
                table: "persons");
        }
    }
}
