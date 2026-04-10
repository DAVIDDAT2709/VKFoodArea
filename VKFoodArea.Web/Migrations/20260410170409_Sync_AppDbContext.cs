using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VKFoodArea.Web.Migrations
{
    /// <inheritdoc />
    public partial class Sync_AppDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserKey",
                table: "NarrationHistories",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserKey",
                table: "NarrationHistories");
        }
    }
}
