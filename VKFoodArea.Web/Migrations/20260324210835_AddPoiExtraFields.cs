using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VKFoodArea.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPoiExtraFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Pois",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Pois",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TtsScriptDe",
                table: "Pois",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TtsScriptJa",
                table: "Pois",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TtsScriptZh",
                table: "Pois",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Pois");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Pois");

            migrationBuilder.DropColumn(
                name: "TtsScriptDe",
                table: "Pois");

            migrationBuilder.DropColumn(
                name: "TtsScriptJa",
                table: "Pois");

            migrationBuilder.DropColumn(
                name: "TtsScriptZh",
                table: "Pois");
        }
    }
}
