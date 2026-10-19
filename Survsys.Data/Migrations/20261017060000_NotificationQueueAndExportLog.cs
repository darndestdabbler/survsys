using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotificationQueueAndExportLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nmi_export_queue",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_condition_id = table.Column<int>(type: "int", nullable: false),
                    queued_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    hold = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nmi_export_queue", x => x.id);
                    table.ForeignKey(
                        name: "FK_nmi_export_queue_person_conditions_person_condition_id",
                        column: x => x.person_condition_id,
                        principalTable: "person_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nmi_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_condition_id = table.Column<int>(type: "int", nullable: false),
                    record_number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    guide_id = table.Column<int>(type: "int", nullable: true),
                    xml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    queue_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    sent_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    detail = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nmi_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_nmi_log_person_conditions_person_condition_id",
                        column: x => x.person_condition_id,
                        principalTable: "person_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_nmi_export_queue_person_condition_id",
                table: "nmi_export_queue",
                column: "person_condition_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nmi_log_person_condition_id",
                table: "nmi_log",
                column: "person_condition_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nmi_export_queue");

            migrationBuilder.DropTable(
                name: "nmi_log");
        }
    }
}
