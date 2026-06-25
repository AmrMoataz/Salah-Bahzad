using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalahBahazad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentSerial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Serial",
                table: "students",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows with a per-row serial BEFORE the unique index is created, so the index is
            // never built over duplicate "" defaults (FR-APP-VID-003). Ids are UUIDv7, whose LEADING hex is a
            // shared, time-ordered timestamp prefix (highly collision-prone) — so derive from the random TAIL:
            // the LAST 6 hex chars of "Id" (all valid Crockford 0-9A-F) give a well-distributed "STU-XXXXXX".
            // The (TenantId, Serial) unique index below is the hard guard: a residual collision fails the
            // migration loudly (the correct, visible outcome). (On a fresh test DB this updates 0 rows.)
            migrationBuilder.Sql(
                "UPDATE students SET \"Serial\" = 'STU-' || upper(right(replace(\"Id\"::text, '-', ''), 6)) " +
                "WHERE \"Serial\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_students_TenantId_Serial",
                table: "students",
                columns: new[] { "TenantId", "Serial" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_students_TenantId_Serial",
                table: "students");

            migrationBuilder.DropColumn(
                name: "Serial",
                table: "students");
        }
    }
}
