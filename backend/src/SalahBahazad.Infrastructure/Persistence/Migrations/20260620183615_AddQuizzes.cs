using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalahBahazad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizzes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserAssignmentId",
                table: "assessment_events",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "QuizAttemptId",
                table: "assessment_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_quizzes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GatedSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeLimitMinutes = table.Column<int>(type: "integer", nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MinPassPercent = table.Column<int>(type: "integer", nullable: false),
                    AttemptsUsed = table.Column<int>(type: "integer", nullable: false),
                    BestPercent = table.Column<int>(type: "integer", nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_quizzes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_quizzes_enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_quizzes_sessions_GatedSessionId",
                        column: x => x.GatedSessionId,
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_quizzes_sessions_SourceSessionId",
                        column: x => x.SourceSessionId,
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_quizzes_students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quiz_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScorePercent = table.Column<int>(type: "integer", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeadlineUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UserQuizId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quiz_attempts_user_quizzes_UserQuizId",
                        column: x => x.UserQuizId,
                        principalTable: "user_quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_attempt_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    BodyLatex = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ImageObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Mark = table.Column<int>(type: "integer", nullable: false),
                    SelectedOptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnsweredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    QuizAttemptId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_attempt_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quiz_attempt_questions_quiz_attempts_QuizAttemptId",
                        column: x => x.QuizAttemptId,
                        principalTable: "quiz_attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_attempt_question_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    QuizAttemptQuestionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_attempt_question_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quiz_attempt_question_options_quiz_attempt_questions_QuizAt~",
                        column: x => x.QuizAttemptQuestionId,
                        principalTable: "quiz_attempt_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assessment_events_TenantId_QuizAttemptId_OccurredAtUtc",
                table: "assessment_events",
                columns: new[] { "TenantId", "QuizAttemptId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_quiz_attempt_question_options_QuizAttemptQuestionId",
                table: "quiz_attempt_question_options",
                column: "QuizAttemptQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_attempt_questions_QuizAttemptId",
                table: "quiz_attempt_questions",
                column: "QuizAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_attempts_UserQuizId",
                table: "quiz_attempts",
                column: "UserQuizId");

            migrationBuilder.CreateIndex(
                name: "IX_user_quizzes_EnrollmentId",
                table: "user_quizzes",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_user_quizzes_GatedSessionId",
                table: "user_quizzes",
                column: "GatedSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_quizzes_SourceSessionId",
                table: "user_quizzes",
                column: "SourceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_quizzes_StudentId",
                table: "user_quizzes",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_user_quizzes_TenantId_EnrollmentId",
                table: "user_quizzes",
                columns: new[] { "TenantId", "EnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_quizzes_TenantId_StudentId_GatedSessionId",
                table: "user_quizzes",
                columns: new[] { "TenantId", "StudentId", "GatedSessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quiz_attempt_question_options");

            migrationBuilder.DropTable(
                name: "quiz_attempt_questions");

            migrationBuilder.DropTable(
                name: "quiz_attempts");

            migrationBuilder.DropTable(
                name: "user_quizzes");

            migrationBuilder.DropIndex(
                name: "IX_assessment_events_TenantId_QuizAttemptId_OccurredAtUtc",
                table: "assessment_events");

            migrationBuilder.DropColumn(
                name: "QuizAttemptId",
                table: "assessment_events");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserAssignmentId",
                table: "assessment_events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
