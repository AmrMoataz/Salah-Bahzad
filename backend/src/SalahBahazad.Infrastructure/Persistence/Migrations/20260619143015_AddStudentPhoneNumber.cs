using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalahBahazad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentPhoneNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "students",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "students");
        }
    }
}
