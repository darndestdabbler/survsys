using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class EcrCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "case_reason",
                table: "ecr_document",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "case_status",
                table: "ecr_document",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ecr_case_link",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ecr_document_id = table.Column<int>(type: "int", nullable: false),
                    person_condition_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ecr_case_link", x => x.id);
                    table.ForeignKey(
                        name: "FK_ecr_case_link_ecr_document_ecr_document_id",
                        column: x => x.ecr_document_id,
                        principalTable: "ecr_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ecr_case_link_person_conditions_person_condition_id",
                        column: x => x.person_condition_id,
                        principalTable: "person_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ecr_case_value",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ecr_case_link_id = table.Column<int>(type: "int", nullable: false),
                    target = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    target_id = table.Column<int>(type: "int", nullable: false),
                    key = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    written = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ecr_case_value", x => x.id);
                    table.ForeignKey(
                        name: "FK_ecr_case_value_ecr_case_link_ecr_case_link_id",
                        column: x => x.ecr_case_link_id,
                        principalTable: "ecr_case_link",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ecr_case_link_ecr_document_id_person_condition_id",
                table: "ecr_case_link",
                columns: new[] { "ecr_document_id", "person_condition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ecr_case_link_person_condition_id",
                table: "ecr_case_link",
                column: "person_condition_id");

            migrationBuilder.CreateIndex(
                name: "IX_ecr_case_value_ecr_case_link_id",
                table: "ecr_case_value",
                column: "ecr_case_link_id");

            migrationBuilder.CreateIndex(
                name: "IX_ecr_case_value_target_target_id",
                table: "ecr_case_value",
                columns: new[] { "target", "target_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ecr_case_value");

            migrationBuilder.DropTable(
                name: "ecr_case_link");

            migrationBuilder.DropColumn(
                name: "case_reason",
                table: "ecr_document");

            migrationBuilder.DropColumn(
                name: "case_status",
                table: "ecr_document");
        }
    }
}
