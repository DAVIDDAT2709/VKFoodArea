using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VKFoodArea.Web.Migrations
{
    public partial class AddAppDevicePresence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppDeviceSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    UserKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    AppVersion = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastHeartbeatAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastOfflineAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDeviceSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppDeviceSessions_DeviceKey",
                table: "AppDeviceSessions",
                column: "DeviceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDeviceSessions_LastHeartbeatAt",
                table: "AppDeviceSessions",
                column: "LastHeartbeatAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppDeviceSessions_UserKey",
                table: "AppDeviceSessions",
                column: "UserKey");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppDeviceSessions");
        }
    }
}