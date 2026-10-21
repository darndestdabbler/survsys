using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class VaccineActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "active",
                table: "vaccines",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "active",
                table: "vaccines");
        }
    }
}
