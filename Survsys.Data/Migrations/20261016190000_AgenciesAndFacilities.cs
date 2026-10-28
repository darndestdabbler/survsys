using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgenciesAndFacilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agencies",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    short_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agencies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "facilities",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    short_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    agency_id = table.Column<int>(type: "int", nullable: false),
                    street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    state = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    zip = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facilities", x => x.id);
                    table.ForeignKey(
                        name: "FK_facilities_agencies_agency_id",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "zip_code_agencies",
                columns: table => new
                {
                    zip_code = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    agency_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zip_code_agencies", x => new { x.zip_code, x.agency_id });
                    table.ForeignKey(
                        name: "FK_zip_code_agencies_agencies_agency_id",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "agencies",
                columns: new[] { "id", "name", "short_name" },
                values: new object[,]
                {
                    { 1, "Bear River", null },
                    { 2, "Central Utah", null },
                    { 3, "Davis County", null },
                    { 4, "Out of State", null },
                    { 5, "Salt Lake County", null },
                    { 6, "San Juan County", null },
                    { 7, "Southeastern Utah", null },
                    { 8, "Southwest Utah", null },
                    { 9, "Summit County", null },
                    { 10, "Tooele County", null },
                    { 11, "TriCounty", null },
                    { 12, "Utah County", null },
                    { 13, "Utah Navajo", null },
                    { 14, "Utah State", null },
                    { 15, "Wasatch County", null },
                    { 16, "Weber-Morgan", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_facilities_agency_id",
                table: "facilities",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "IX_zip_code_agencies_agency_id",
                table: "zip_code_agencies",
                column: "agency_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "facilities");

            migrationBuilder.DropTable(
                name: "zip_code_agencies");

            migrationBuilder.DropTable(
                name: "agencies");
        }
    }
}
