using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class EcrSenders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "ecr_document",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sender",
                table: "ecr_document",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ecr_sender",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    sender_id = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    stage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    takes_lab_results = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ecr_sender", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ecr_document_sender",
                table: "ecr_document",
                column: "sender");

            migrationBuilder.CreateIndex(
                name: "IX_ecr_sender_sender_id",
                table: "ecr_sender",
                column: "sender_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ecr_sender");

            migrationBuilder.DropIndex(
                name: "IX_ecr_document_sender",
                table: "ecr_document");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "ecr_document");

            migrationBuilder.DropColumn(
                name: "sender",
                table: "ecr_document");
        }
    }
}
