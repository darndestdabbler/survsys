using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class Providers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "providers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    provider_type_id = table.Column<int>(type: "int", nullable: true),
                    first_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    middle_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_providers", x => x.id);
                    table.ForeignKey(
                        name: "FK_providers_code_values_provider_type_id",
                        column: x => x.provider_type_id,
                        principalTable: "code_values",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "code_values",
                columns: new[] { "id", "active", "code", "description", "fips", "kind", "list", "sort_order", "state" },
                values: new object[] { 1175, true, "clinician", "Clinician", null, null, "provider_type", 1, null });

            migrationBuilder.CreateIndex(
                name: "IX_providers_last_name",
                table: "providers",
                column: "last_name");

            migrationBuilder.CreateIndex(
                name: "IX_providers_provider_type_id",
                table: "providers",
                column: "provider_type_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "providers");

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1175);
        }
    }
}
