using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class JurisdictionAccessAndLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_sign_in_count",
                table: "users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_out_until",
                table: "users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_agencies",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    agency_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_agencies", x => new { x.user_id, x.agency_id });
                    table.ForeignKey(
                        name: "FK_user_agencies_agencies_agency_id",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_agencies_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_agencies_agency_id",
                table: "user_agencies",
                column: "agency_id");

            migrationBuilder.InsertData(
                table: "role_privileges",
                columns: new[] { "privilege", "role_id" },
                values: new object[] { "all_jurisdictions", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "role_privileges",
                keyColumns: new[] { "privilege", "role_id" },
                keyValues: new object[] { "all_jurisdictions", 1 });

            migrationBuilder.DropTable(
                name: "user_agencies");

            migrationBuilder.DropColumn(
                name: "failed_sign_in_count",
                table: "users");

            migrationBuilder.DropColumn(
                name: "locked_out_until",
                table: "users");
        }
    }
}
