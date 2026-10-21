using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotificationReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "retract",
                table: "nmi_export_queue",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "nmi_condition_print_criteria",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    condition_id = table.Column<int>(type: "int", nullable: false),
                    case_status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nmi_condition_print_criteria", x => x.id);
                    table.ForeignKey(
                        name: "FK_nmi_condition_print_criteria_conditions_condition_id",
                        column: x => x.condition_id,
                        principalTable: "conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nmi_reconciliation_cutoff",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    mmwr_year = table.Column<int>(type: "int", nullable: false),
                    mmwr_week = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nmi_reconciliation_cutoff", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_nmi_condition_print_criteria_condition_id_case_status",
                table: "nmi_condition_print_criteria",
                columns: new[] { "condition_id", "case_status" },
                unique: true);

            // Every condition already here notifies its confirmed and probable
            // cases, so the auto-queue keeps queueing what it did before the
            // criteria existed until an administrator narrows them.
            migrationBuilder.Sql(
                "INSERT INTO nmi_condition_print_criteria (condition_id, case_status) "
                + "SELECT id, 'C' FROM conditions UNION ALL SELECT id, 'P' FROM conditions;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nmi_condition_print_criteria");

            migrationBuilder.DropTable(
                name: "nmi_reconciliation_cutoff");

            migrationBuilder.DropColumn(
                name: "retract",
                table: "nmi_export_queue");
        }
    }
}
