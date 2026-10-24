using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class CaseClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "case_status",
                table: "person_conditions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "mmwr_week",
                table: "person_conditions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "mmwr_year",
                table: "person_conditions",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "case_status",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "mmwr_week",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "mmwr_year",
                table: "person_conditions");
        }
    }
}
