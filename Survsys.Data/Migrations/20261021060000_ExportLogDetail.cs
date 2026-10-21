using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExportLogDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "processing_id",
                table: "nmi_log",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "queue",
                table: "nmi_log",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "nmi_log_detail",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    log_id = table.Column<int>(type: "int", nullable: false),
                    phin_var = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nmi_log_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_nmi_log_detail_nmi_log_log_id",
                        column: x => x.log_id,
                        principalTable: "nmi_log",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_nmi_log_detail_log_id",
                table: "nmi_log_detail",
                column: "log_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nmi_log_detail");

            migrationBuilder.DropColumn(
                name: "processing_id",
                table: "nmi_log");

            migrationBuilder.DropColumn(
                name: "queue",
                table: "nmi_log");
        }
    }
}
