using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class InvestigationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "investigator_id",
                table: "person_conditions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "workflow_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    person_condition_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    transition = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    from_state = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    to_state = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_history_person_conditions_person_condition_id",
                        column: x => x.person_condition_id,
                        principalTable: "person_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workflow_history_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_person_conditions_investigator_id",
                table: "person_conditions",
                column: "investigator_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_history_person_condition_id",
                table: "workflow_history",
                column: "person_condition_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_history_user_id",
                table: "workflow_history",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_person_conditions_users_investigator_id",
                table: "person_conditions",
                column: "investigator_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // The three privileges that gate the new transitions, granted to
            // the Administrator role (id 1) as run_reports was: the role that
            // holds every privilege keeps holding every privilege.
            migrationBuilder.InsertData(
                table: "role_privileges",
                columns: new[] { "privilege", "role_id" },
                values: new object[,]
                {
                    { "route_cases", 1 },
                    { "assign_investigators", 1 },
                    { "approve_cases", 1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "role_privileges",
                keyColumns: new[] { "privilege", "role_id" },
                keyValues: new object[] { "route_cases", 1 });

            migrationBuilder.DeleteData(
                table: "role_privileges",
                keyColumns: new[] { "privilege", "role_id" },
                keyValues: new object[] { "assign_investigators", 1 });

            migrationBuilder.DeleteData(
                table: "role_privileges",
                keyColumns: new[] { "privilege", "role_id" },
                keyValues: new object[] { "approve_cases", 1 });

            migrationBuilder.DropForeignKey(
                name: "FK_person_conditions_users_investigator_id",
                table: "person_conditions");

            migrationBuilder.DropTable(
                name: "workflow_history");

            migrationBuilder.DropIndex(
                name: "IX_person_conditions_investigator_id",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "investigator_id",
                table: "person_conditions");
        }
    }
}
