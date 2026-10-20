using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersonIdentifiersAndAddressHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddressRules.CloseHistoric over the rows already on file, so the
            // one-current-address index below can be built: the open address
            // that began last stays current, and the others end the day it
            // began, or today where it has no beginning.
            migrationBuilder.Sql("""
                WITH open_addresses AS (
                    SELECT id, person_id, beginning,
                        ROW_NUMBER() OVER (PARTITION BY person_id ORDER BY beginning DESC, id DESC) AS position
                    FROM person_addresses
                    WHERE ending IS NULL)
                UPDATE surplus
                SET ending = COALESCE(kept.beginning, CAST(GETDATE() AS date))
                FROM person_addresses surplus
                JOIN open_addresses closing ON closing.id = surplus.id AND closing.position > 1
                JOIN open_addresses kept ON kept.person_id = closing.person_id AND kept.position = 1;
                """);

            migrationBuilder.CreateTable(
                name: "person_identifiers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    identifier_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    value = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    assigning_authority = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    assigning_authority_type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_identifiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_person_identifiers_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_name_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    last_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    first_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    middle_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    changed_on = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_name_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_person_name_history_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zip_codes",
                columns: table => new
                {
                    code = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    county = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zip_codes", x => x.code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_person_addresses_person_id_current",
                table: "person_addresses",
                column: "person_id",
                unique: true,
                filter: "[ending] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_person_identifiers_person_id",
                table: "person_identifiers",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_identifiers_value",
                table: "person_identifiers",
                column: "value");

            migrationBuilder.CreateIndex(
                name: "IX_person_name_history_person_id",
                table: "person_name_history",
                column: "person_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "person_identifiers");

            migrationBuilder.DropTable(
                name: "person_name_history");

            migrationBuilder.DropTable(
                name: "zip_codes");

            migrationBuilder.DropIndex(
                name: "IX_person_addresses_person_id_current",
                table: "person_addresses");
        }
    }
}
