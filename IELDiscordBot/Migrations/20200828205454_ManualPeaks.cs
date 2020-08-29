using Microsoft.EntityFrameworkCore.Migrations;

namespace IELDiscordBotPOC.Migrations
{
    public partial class ManualPeaks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManualPeakOverrides",
                columns: table => new
                {
                    Platform = table.Column<string>(maxLength: 10, nullable: false),
                    User = table.Column<string>(maxLength: 50, nullable: false),
                    Season = table.Column<int>(nullable: false),
                    Peak = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualPeakOverrides", x => new { x.Platform, x.User, x.Season });
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualPeakOverrides");
        }
    }
}
