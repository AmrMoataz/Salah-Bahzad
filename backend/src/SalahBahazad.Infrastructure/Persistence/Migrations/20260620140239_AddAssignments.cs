using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalahBahazad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScoreMarks = table.Column<int>(type: "integer", nullable: true),
                    MaxMarks = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: true),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    TimeSpentSeconds = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_assignments_enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_assignments_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_assignments_students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assessment_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    QuestionOrder = table.Column<int>(type: "integer", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessment_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assessment_events_user_assignments_UserAssignmentId",
                        column: x => x.UserAssignmentId,
                        principalTable: "user_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assignment_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    BodyLatex = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ImageObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Mark = table.Column<int>(type: "integer", nullable: false),
                    HintUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SelectedOptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnsweredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UserAssignmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assignment_questions_user_assignments_UserAssignmentId",
                        column: x => x.UserAssignmentId,
                        principalTable: "user_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assignment_question_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    AssignmentQuestionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_question_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assignment_question_options_assignment_questions_Assignment~",
                        column: x => x.AssignmentQuestionId,
                        principalTable: "assignment_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assessment_events_TenantId_UserAssignmentId_OccurredAtUtc",
                table: "assessment_events",
                columns: new[] { "TenantId", "UserAssignmentId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_assessment_events_UserAssignmentId",
                table: "assessment_events",
                column: "UserAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_assignment_question_options_AssignmentQuestionId",
                table: "assignment_question_options",
                column: "AssignmentQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_assignment_questions_UserAssignmentId",
                table: "assignment_questions",
                column: "UserAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_user_assignments_EnrollmentId",
                table: "user_assignments",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_user_assignments_SessionId_Status",
                table: "user_assignments",
                columns: new[] { "SessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_user_assignments_StudentId",
                table: "user_assignments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_user_assignments_TenantId_EnrollmentId",
                table: "user_assignments",
                columns: new[] { "TenantId", "EnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_assignments_TenantId_StudentId_SessionId",
                table: "user_assignments",
                columns: new[] { "TenantId", "StudentId", "SessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assessment_events");

            migrationBuilder.DropTable(
                name: "assignment_question_options");

            migrationBuilder.DropTable(
                name: "assignment_questions");

            migrationBuilder.DropTable(
                name: "user_assignments");
        }
    }
}
