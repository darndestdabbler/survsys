using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class ParsedMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "accession_no",
                table: "system_messages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "collected_on",
                table: "system_messages",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "message_format",
                table: "system_messages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "HL7v2");

            migrationBuilder.AddColumn<string>(
                name: "message_type",
                table: "system_messages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "observed_on",
                table: "system_messages",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "order_index",
                table: "system_messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "patient_birth_date",
                table: "system_messages",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_city",
                table: "system_messages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_first_name",
                table: "system_messages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_last_name",
                table: "system_messages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_middle_name",
                table: "system_messages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_sex",
                table: "system_messages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_state",
                table: "system_messages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_street",
                table: "system_messages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_zip",
                table: "system_messages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sending_facility",
                table: "system_messages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "specimen_source_code",
                table: "system_messages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "specimen_source_text",
                table: "system_messages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "test_code",
                table: "system_messages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "test_name",
                table: "system_messages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "system_message_results",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    system_message_id = table.Column<int>(type: "int", nullable: false),
                    sequence = table.Column<int>(type: "int", nullable: false),
                    observation_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    observation_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    result_value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    coded_result = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    units = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    reference_range = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    abnormal_flag = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    result_status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_message_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_system_message_results_system_messages_system_message_id",
                        column: x => x.system_message_id,
                        principalTable: "system_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_system_message_results_system_message_id",
                table: "system_message_results",
                column: "system_message_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_message_results");

            migrationBuilder.DropColumn(
                name: "accession_no",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "collected_on",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "message_format",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "message_type",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "observed_on",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "order_index",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "patient_birth_date",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "patient_city",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "patient_first_name",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "patient_last_name",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "patient_middle_name",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "patient_sex",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "patient_state",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "patient_street",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "patient_zip",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "sending_facility",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "specimen_source_code",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "specimen_source_text",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "test_code",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "test_name",
                table: "system_messages");
        }
    }
}
