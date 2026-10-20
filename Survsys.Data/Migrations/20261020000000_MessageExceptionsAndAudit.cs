using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class MessageExceptionsAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "exception_category",
                table: "system_messages",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "notified_at",
                table: "system_messages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retry_count",
                table: "system_messages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "notification_emails",
                table: "agencies",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "system_messages_audits",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    system_message_id = table.Column<int>(type: "int", nullable: false),
                    final_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    exception_category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    exception_detail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_messages_audits", x => x.id);
                    table.ForeignKey(
                        name: "FK_system_messages_audits_system_messages_system_message_id",
                        column: x => x.system_message_id,
                        principalTable: "system_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_system_messages_audits_system_message_id",
                table: "system_messages_audits",
                column: "system_message_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_messages_audits");

            migrationBuilder.DropColumn(
                name: "exception_category",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "notified_at",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "retry_count",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "notification_emails",
                table: "agencies");
        }
    }
}
