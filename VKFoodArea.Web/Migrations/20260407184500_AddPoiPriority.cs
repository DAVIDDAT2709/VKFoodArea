using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VKFoodArea.Web.Data;

#nullable disable

namespace VKFoodArea.Web.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260407184500_AddPoiPriority")]
    public partial class AddPoiPriority : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Pois",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Pois");
        }
    }
}
