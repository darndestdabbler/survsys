using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class Forms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "forms",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    condition_id = table.Column<int>(type: "int", nullable: false),
                    auto_assign = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forms", x => x.id);
                    table.ForeignKey(
                        name: "FK_forms_conditions_condition_id",
                        column: x => x.condition_id,
                        principalTable: "conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "form_assignments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    form_id = table.Column<int>(type: "int", nullable: false),
                    person_condition_id = table.Column<int>(type: "int", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_form_assignments_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_form_assignments_person_conditions_person_condition_id",
                        column: x => x.person_condition_id,
                        principalTable: "person_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "form_sections",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    form_id = table.Column<int>(type: "int", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form_sections", x => x.id);
                    table.ForeignKey(
                        name: "FK_form_sections_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "form_questions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    form_section_id = table.Column<int>(type: "int", nullable: false),
                    text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    help_text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    answer_type = table.Column<int>(type: "int", nullable: false),
                    is_required = table.Column<bool>(type: "bit", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_form_questions_form_sections_form_section_id",
                        column: x => x.form_section_id,
                        principalTable: "form_sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "form_answers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    form_assignment_id = table.Column<int>(type: "int", nullable: false),
                    form_question_id = table.Column<int>(type: "int", nullable: false),
                    text_answer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form_answers", x => x.id);
                    table.ForeignKey(
                        name: "FK_form_answers_form_assignments_form_assignment_id",
                        column: x => x.form_assignment_id,
                        principalTable: "form_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_form_answers_form_questions_form_question_id",
                        column: x => x.form_question_id,
                        principalTable: "form_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "form_question_options",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    form_question_id = table.Column<int>(type: "int", nullable: false),
                    code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_form_question_options", x => x.id);
                    table.ForeignKey(
                        name: "FK_form_question_options_form_questions_form_question_id",
                        column: x => x.form_question_id,
                        principalTable: "form_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_form_answers_form_assignment_id_form_question_id",
                table: "form_answers",
                columns: new[] { "form_assignment_id", "form_question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_form_answers_form_question_id",
                table: "form_answers",
                column: "form_question_id");

            migrationBuilder.CreateIndex(
                name: "IX_form_assignments_form_id_person_condition_id",
                table: "form_assignments",
                columns: new[] { "form_id", "person_condition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_form_assignments_person_condition_id",
                table: "form_assignments",
                column: "person_condition_id");

            migrationBuilder.CreateIndex(
                name: "IX_form_question_options_form_question_id",
                table: "form_question_options",
                column: "form_question_id");

            migrationBuilder.CreateIndex(
                name: "IX_form_questions_form_section_id",
                table: "form_questions",
                column: "form_section_id");

            migrationBuilder.CreateIndex(
                name: "IX_form_sections_form_id",
                table: "form_sections",
                column: "form_id");

            migrationBuilder.CreateIndex(
                name: "IX_forms_condition_id",
                table: "forms",
                column: "condition_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "form_answers");

            migrationBuilder.DropTable(
                name: "form_question_options");

            migrationBuilder.DropTable(
                name: "form_assignments");

            migrationBuilder.DropTable(
                name: "form_questions");

            migrationBuilder.DropTable(
                name: "form_sections");

            migrationBuilder.DropTable(
                name: "forms");
        }
    }
}
