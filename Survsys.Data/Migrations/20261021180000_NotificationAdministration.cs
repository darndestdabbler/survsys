using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotificationAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "version",
                table: "guide",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "nmi_condition_lab_override",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    condition_id = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    item_id = table.Column<int>(type: "int", nullable: false),
                    include = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nmi_condition_lab_override", x => x.id);
                    table.ForeignKey(
                        name: "FK_nmi_condition_lab_override_conditions_condition_id",
                        column: x => x.condition_id,
                        principalTable: "conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nmi_guide_variable_default",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    default_value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nmi_guide_variable_default", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nmi_hep_lab_group",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    group_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nmi_hep_lab_group", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nmi_phin_unit_of_measure",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    value_set_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    concept_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    preferred_concept_name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    hl7_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    code_system_oid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nmi_phin_unit_of_measure", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nmi_phin_value_set",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    value_set_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    concept_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    preferred_concept_name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    hl7_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    code_system_oid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    cdc_name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nmi_phin_value_set", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nmi_guide_variable",
                columns: table => new
                {
                    guide_id = table.Column<int>(type: "int", nullable: false),
                    guide_variable_default_id = table.Column<int>(type: "int", nullable: false),
                    value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nmi_guide_variable", x => new { x.guide_id, x.guide_variable_default_id });
                    table.ForeignKey(
                        name: "FK_nmi_guide_variable_guide_guide_id",
                        column: x => x.guide_id,
                        principalTable: "guide",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_nmi_guide_variable_nmi_guide_variable_default_guide_variable_default_id",
                        column: x => x.guide_variable_default_id,
                        principalTable: "nmi_guide_variable_default",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nmi_hep_lab_group_condition",
                columns: table => new
                {
                    condition_id = table.Column<int>(type: "int", nullable: false),
                    hep_lab_group_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nmi_hep_lab_group_condition", x => x.condition_id);
                    table.ForeignKey(
                        name: "FK_nmi_hep_lab_group_condition_conditions_condition_id",
                        column: x => x.condition_id,
                        principalTable: "conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_nmi_hep_lab_group_condition_nmi_hep_lab_group_hep_lab_group_id",
                        column: x => x.hep_lab_group_id,
                        principalTable: "nmi_hep_lab_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nmi_hep_lab_group_test_type",
                columns: table => new
                {
                    hep_lab_group_id = table.Column<int>(type: "int", nullable: false),
                    lab_test_type = table.Column<int>(type: "int", nullable: false),
                    test_group_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nmi_hep_lab_group_test_type", x => new { x.hep_lab_group_id, x.lab_test_type });
                    table.ForeignKey(
                        name: "FK_nmi_hep_lab_group_test_type_nmi_hep_lab_group_hep_lab_group_id",
                        column: x => x.hep_lab_group_id,
                        principalTable: "nmi_hep_lab_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_nmi_condition_lab_override_condition_id_kind_item_id",
                table: "nmi_condition_lab_override",
                columns: new[] { "condition_id", "kind", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nmi_guide_variable_guide_variable_default_id",
                table: "nmi_guide_variable",
                column: "guide_variable_default_id");

            migrationBuilder.CreateIndex(
                name: "IX_nmi_guide_variable_default_name",
                table: "nmi_guide_variable_default",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nmi_hep_lab_group_group_name",
                table: "nmi_hep_lab_group",
                column: "group_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nmi_hep_lab_group_condition_hep_lab_group_id",
                table: "nmi_hep_lab_group_condition",
                column: "hep_lab_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_nmi_phin_unit_of_measure_value_set_name_concept_code",
                table: "nmi_phin_unit_of_measure",
                columns: new[] { "value_set_name", "concept_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nmi_phin_value_set_value_set_name_concept_code",
                table: "nmi_phin_value_set",
                columns: new[] { "value_set_name", "concept_code" },
                unique: true);

            // The header every guide used while it was hard-coded in the
            // message builder, so a guide that sets none keeps sending it.
            migrationBuilder.InsertData(
                table: "nmi_guide_variable_default",
                columns: new[] { "name", "default_value" },
                values: new object[,]
                {
                    { "sending_app", "Survsys^2.16.840.1.114222.4.3.3.2.1^ISO" },
                    { "sending_facility", "CTDPH^2.16.840.1.114222.4.1.3650^ISO" },
                    { "receiving_app", "CDC^2.16.840.1.114222.4.3.2.10^ISO" },
                    { "receiving_facility", "CDC^2.16.840.1.114222.4.1.3650^ISO" },
                    { "hl7_version", "2.5.1" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nmi_condition_lab_override");

            migrationBuilder.DropTable(
                name: "nmi_guide_variable");

            migrationBuilder.DropTable(
                name: "nmi_hep_lab_group_condition");

            migrationBuilder.DropTable(
                name: "nmi_hep_lab_group_test_type");

            migrationBuilder.DropTable(
                name: "nmi_phin_unit_of_measure");

            migrationBuilder.DropTable(
                name: "nmi_phin_value_set");

            migrationBuilder.DropTable(
                name: "nmi_guide_variable_default");

            migrationBuilder.DropTable(
                name: "nmi_hep_lab_group");

            migrationBuilder.DropColumn(
                name: "version",
                table: "guide");
        }
    }
}
