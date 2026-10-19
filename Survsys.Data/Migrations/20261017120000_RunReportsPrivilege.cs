using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class RunReportsPrivilege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "role_privileges",
                columns: new[] { "privilege", "role_id" },
                values: new object[] { "run_reports", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "role_privileges",
                keyColumns: new[] { "privilege", "role_id" },
                keyValues: new object[] { "run_reports", 1 });
        }
    }
}
