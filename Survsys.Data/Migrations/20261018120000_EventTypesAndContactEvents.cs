using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class EventTypesAndContactEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "event_type",
                table: "person_conditions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "morbidity");

            migrationBuilder.AddColumn<int>(
                name: "person_condition_id",
                table: "exposure_contacts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_exposure_contacts_person_condition_id",
                table: "exposure_contacts",
                column: "person_condition_id");

            migrationBuilder.AddForeignKey(
                name: "FK_exposure_contacts_person_conditions_person_condition_id",
                table: "exposure_contacts",
                column: "person_condition_id",
                principalTable: "person_conditions",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exposure_contacts_person_conditions_person_condition_id",
                table: "exposure_contacts");

            migrationBuilder.DropIndex(
                name: "IX_exposure_contacts_person_condition_id",
                table: "exposure_contacts");

            migrationBuilder.DropColumn(
                name: "event_type",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "person_condition_id",
                table: "exposure_contacts");
        }
    }
}
