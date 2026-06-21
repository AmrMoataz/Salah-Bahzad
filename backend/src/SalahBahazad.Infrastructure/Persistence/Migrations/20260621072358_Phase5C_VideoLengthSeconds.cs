using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalahBahazad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5C_VideoLengthSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LengthMinutes",
                table: "session_videos",
                newName: "LengthSeconds");

            // Existing values were whole-minute admin estimates; convert to seconds so they still read as MM:SS
            // until the video is (re-)transcoded and the exact ffprobe duration replaces them.
            migrationBuilder.Sql("UPDATE session_videos SET \"LengthSeconds\" = \"LengthSeconds\" * 60;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LengthSeconds",
                table: "session_videos",
                newName: "LengthMinutes");

            migrationBuilder.Sql("UPDATE session_videos SET \"LengthMinutes\" = \"LengthMinutes\" / 60;");
        }
    }
}
