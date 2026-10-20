using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersonMerges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "merged_at",
                table: "persons",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "merged_into_person_id",
                table: "persons",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_persons_merged_into_person_id",
                table: "persons",
                column: "merged_into_person_id");

            migrationBuilder.AddForeignKey(
                name: "FK_persons_persons_merged_into_person_id",
                table: "persons",
                column: "merged_into_person_id",
                principalTable: "persons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // The privilege that gates finding and merging duplicate persons,
            // granted to the Administrator role (id 1): the role that holds
            // every privilege keeps holding every privilege.
            migrationBuilder.InsertData(
                table: "role_privileges",
                columns: new[] { "privilege", "role_id" },
                values: new object[] { "merge_persons", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "role_privileges",
                keyColumns: new[] { "privilege", "role_id" },
                keyValues: new object[] { "merge_persons", 1 });

            migrationBuilder.DropForeignKey(
                name: "FK_persons_persons_merged_into_person_id",
                table: "persons");

            migrationBuilder.DropIndex(
                name: "IX_persons_merged_into_person_id",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "merged_at",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "merged_into_person_id",
                table: "persons");
        }
    }
}
