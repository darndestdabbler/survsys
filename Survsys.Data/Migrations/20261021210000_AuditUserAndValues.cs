using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditUserAndValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "new_values",
                table: "audit_entries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "old_values",
                table: "audit_entries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "audit_entries",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "person_condition_id",
                table: "access_records",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "action",
                table: "access_records",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "View");

            migrationBuilder.AddColumn<string>(
                name: "detail",
                table: "access_records",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "new_values",
                table: "audit_entries");

            migrationBuilder.DropColumn(
                name: "old_values",
                table: "audit_entries");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "audit_entries");

            migrationBuilder.DropColumn(
                name: "action",
                table: "access_records");

            migrationBuilder.DropColumn(
                name: "detail",
                table: "access_records");

            migrationBuilder.AlterColumn<int>(
                name: "person_condition_id",
                table: "access_records",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
