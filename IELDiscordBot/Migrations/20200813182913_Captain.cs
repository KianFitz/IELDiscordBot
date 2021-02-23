using Microsoft.EntityFrameworkCore.Migrations;

namespace IELDiscordBot.Migrations
{
    public partial class Captain : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaptainID",
                table: "Teams",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaptainID",
                table: "Teams");
        }
    }
}
