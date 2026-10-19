using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class Persons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "persons",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    last_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    first_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    middle_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    days_old_no_birthday = table.Column<int>(type: "int", nullable: true),
                    birth_gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    current_gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ethnicity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    language = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    country_of_birth = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parent_guardians",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    child_id = table.Column<int>(type: "int", nullable: false),
                    parent_id = table.Column<int>(type: "int", nullable: true),
                    parent_guardian = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parent_guardians", x => x.id);
                    table.ForeignKey(
                        name: "FK_parent_guardians_persons_child_id",
                        column: x => x.child_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_addresses",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    unit_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    city = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    state = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    postal_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    county = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    beginning = table.Column<DateOnly>(type: "date", nullable: true),
                    ending = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    residence_type_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_addresses", x => x.id);
                    table.ForeignKey(
                        name: "FK_person_addresses_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_aliases",
                columns: table => new
                {
                    person_id = table.Column<int>(type: "int", nullable: false),
                    person_alias = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_aliases", x => new { x.person_id, x.person_alias });
                    table.ForeignKey(
                        name: "FK_person_aliases_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_emails",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    email_address = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    current_email = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_emails", x => x.id);
                    table.ForeignKey(
                        name: "FK_person_emails_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_races",
                columns: table => new
                {
                    person_id = table.Column<int>(type: "int", nullable: false),
                    race = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_races", x => new { x.person_id, x.race });
                    table.ForeignKey(
                        name: "FK_person_races_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_telephones",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_id = table.Column<int>(type: "int", nullable: false),
                    telephone_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    country_code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    area_code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    phone_number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    extension = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    beginning = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_telephones", x => x.id);
                    table.ForeignKey(
                        name: "FK_person_telephones_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_parent_guardians_child_id",
                table: "parent_guardians",
                column: "child_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_addresses_person_id",
                table: "person_addresses",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_emails_person_id",
                table: "person_emails",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_telephones_person_id",
                table: "person_telephones",
                column: "person_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parent_guardians");

            migrationBuilder.DropTable(
                name: "person_addresses");

            migrationBuilder.DropTable(
                name: "person_aliases");

            migrationBuilder.DropTable(
                name: "person_emails");

            migrationBuilder.DropTable(
                name: "person_races");

            migrationBuilder.DropTable(
                name: "person_telephones");

            migrationBuilder.DropTable(
                name: "persons");
        }
    }
}
