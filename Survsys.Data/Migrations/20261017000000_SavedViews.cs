using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class SavedViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_views",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    columns = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    condition_id = table.Column<int>(type: "int", nullable: true),
                    agency_id = table.Column<int>(type: "int", nullable: true),
                    workflow_state = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    case_status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    onset_from = table.Column<DateOnly>(type: "date", nullable: true),
                    onset_to = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_views", x => x.id);
                    table.ForeignKey(
                        name: "FK_saved_views_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saved_views_user_id_name",
                table: "saved_views",
                columns: new[] { "user_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_views");
        }
    }
}
