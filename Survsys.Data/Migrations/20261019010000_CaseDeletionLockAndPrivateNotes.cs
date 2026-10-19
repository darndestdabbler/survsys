using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class CaseDeletionLockAndPrivateNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "person_conditions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_at",
                table: "person_conditions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "locked_by_user_id",
                table: "person_conditions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "private",
                table: "notes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_person_conditions_locked_by_user_id",
                table: "person_conditions",
                column: "locked_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_person_conditions_users_locked_by_user_id",
                table: "person_conditions",
                column: "locked_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_person_conditions_users_locked_by_user_id",
                table: "person_conditions");

            migrationBuilder.DropIndex(
                name: "IX_person_conditions_locked_by_user_id",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "locked_at",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "locked_by_user_id",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "private",
                table: "notes");
        }
    }
}
