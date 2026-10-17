using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContactsAndExposures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exposure_places",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    exposure_place_type_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    zip = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    state = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exposure_places", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "exposures",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_condition_id = table.Column<int>(type: "int", nullable: true),
                    condition_id = table.Column<int>(type: "int", nullable: true),
                    agency_id = table.Column<int>(type: "int", nullable: true),
                    exposure_place_id = table.Column<int>(type: "int", nullable: true),
                    exposure_start = table.Column<DateOnly>(type: "date", nullable: true),
                    exposure_end = table.Column<DateOnly>(type: "date", nullable: true),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exposures", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "exposure_contacts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    exposure_id = table.Column<int>(type: "int", nullable: false),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    exposure_start = table.Column<DateOnly>(type: "date", nullable: true),
                    exposure_end = table.Column<DateOnly>(type: "date", nullable: true),
                    disposition_id = table.Column<int>(type: "int", nullable: true),
                    contact_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    disposition_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exposure_contacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_exposure_contacts_exposures_exposure_id",
                        column: x => x.exposure_id,
                        principalTable: "exposures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exposure_contacts_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exposure_sources",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    exposure_id = table.Column<int>(type: "int", nullable: false),
                    person_id = table.Column<int>(type: "int", nullable: true),
                    person_condition_id = table.Column<int>(type: "int", nullable: true),
                    source_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    source_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exposure_sources", x => x.id);
                    table.ForeignKey(
                        name: "FK_exposure_sources_exposures_exposure_id",
                        column: x => x.exposure_id,
                        principalTable: "exposures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exposure_contacts_exposure_id",
                table: "exposure_contacts",
                column: "exposure_id");

            migrationBuilder.CreateIndex(
                name: "IX_exposure_contacts_person_id",
                table: "exposure_contacts",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_exposure_sources_exposure_id",
                table: "exposure_sources",
                column: "exposure_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exposure_contacts");

            migrationBuilder.DropTable(
                name: "exposure_places");

            migrationBuilder.DropTable(
                name: "exposure_sources");

            migrationBuilder.DropTable(
                name: "exposures");
        }
    }
}
