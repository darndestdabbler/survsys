using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class InvestigationAndAdministrative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "cdc_first_verbally_notified_date",
                table: "person_conditions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "day_care_association",
                table: "person_conditions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "event_name",
                table: "person_conditions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "food_handler",
                table: "person_conditions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_living",
                table: "person_conditions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "healthcare_worker",
                table: "person_conditions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "imported_from_id",
                table: "person_conditions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "investigation_outcome_disposition_id",
                table: "person_conditions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lhd_case_status_id",
                table: "person_conditions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "lhd_investigation_completed_date",
                table: "person_conditions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "lhd_investigation_start_date",
                table: "person_conditions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "other_data_1",
                table: "person_conditions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "other_data_2",
                table: "person_conditions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "outbreak_associated",
                table: "person_conditions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "results_reported_to_clinician_date",
                table: "person_conditions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "risk_factors",
                table: "person_conditions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "risk_factors_notes",
                table: "person_conditions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "state_case_status_id",
                table: "person_conditions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transmission_mode",
                table: "person_conditions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cdc_first_verbally_notified_date",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "day_care_association",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "event_name",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "food_handler",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "group_living",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "healthcare_worker",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "imported_from_id",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "investigation_outcome_disposition_id",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "lhd_case_status_id",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "lhd_investigation_completed_date",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "lhd_investigation_start_date",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "other_data_1",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "other_data_2",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "outbreak_associated",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "results_reported_to_clinician_date",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "risk_factors",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "risk_factors_notes",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "state_case_status_id",
                table: "person_conditions");

            migrationBuilder.DropColumn(
                name: "transmission_mode",
                table: "person_conditions");
        }
    }
}
