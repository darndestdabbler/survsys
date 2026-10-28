using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class IbisExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ibis_updated_at",
                table: "person_conditions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sent_to_ibis",
                table: "person_conditions",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ibis_updated_at",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "sent_to_ibis",
                table: "person_conditions");
        }
    }
}
