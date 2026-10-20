using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class OutbreakAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "person_condition_id",
                table: "attachments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "outbreak_id",
                table: "attachments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_attachments_outbreak_id",
                table: "attachments",
                column: "outbreak_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_attachments_one_owner",
                table: "attachments",
                sql: "(person_condition_id IS NULL AND outbreak_id IS NOT NULL) OR (person_condition_id IS NOT NULL AND outbreak_id IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_attachments_outbreaks_outbreak_id",
                table: "attachments",
                column: "outbreak_id",
                principalTable: "outbreaks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attachments_outbreaks_outbreak_id",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "IX_attachments_outbreak_id",
                table: "attachments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_attachments_one_owner",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "outbreak_id",
                table: "attachments");

            migrationBuilder.AlterColumn<int>(
                name: "person_condition_id",
                table: "attachments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
