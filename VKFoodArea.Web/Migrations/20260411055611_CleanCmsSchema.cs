using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VKFoodArea.Web.Migrations
{
    /// <inheritdoc />
    public partial class CleanCmsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "NarrationHistories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "NarrationHistories",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "NarrationHistories",
                type: "REAL",
                nullable: true);

            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS AppUserAccounts (
                    Id INTEGER NOT NULL CONSTRAINT PK_AppUserAccounts PRIMARY KEY AUTOINCREMENT,
                    UserKey TEXT NOT NULL,
                    Username TEXT NOT NULL DEFAULT '',
                    Email TEXT NOT NULL DEFAULT '',
                    FullName TEXT NOT NULL DEFAULT '',
                    NarrationLanguage TEXT NOT NULL DEFAULT 'vi',
                    NarrationPlaybackMode TEXT NOT NULL DEFAULT 'TTS',
                    Role TEXT NOT NULL DEFAULT 'User',
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    LastSeenAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    LastSyncedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                """);

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_AppUserAccounts_UserKey ON AppUserAccounts (UserKey);");

            migrationBuilder.CreateTable(
                name: "PoiAudioAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PoiId = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    FileUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoiAudioAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoiAudioAssets_Pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "Pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoiTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PoiId = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Script = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoiTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoiTranslations_Pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "Pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserMovementLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    AccuracyMeters = table.Column<double>(type: "REAL", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMovementLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TourStops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TourId = table.Column<int>(type: "INTEGER", nullable: false),
                    PoiId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TourStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TourStops_Pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "Pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TourStops_Tours_TourId",
                        column: x => x.TourId,
                        principalTable: "Tours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PoiAudioAssets_PoiId_Language",
                table: "PoiAudioAssets",
                columns: new[] { "PoiId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoiTranslations_PoiId_Language",
                table: "PoiTranslations",
                columns: new[] { "PoiId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tours_Name",
                table: "Tours",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_TourStops_PoiId",
                table: "TourStops",
                column: "PoiId");

            migrationBuilder.CreateIndex(
                name: "IX_TourStops_TourId_DisplayOrder",
                table: "TourStops",
                columns: new[] { "TourId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMovementLogs_RecordedAt",
                table: "UserMovementLogs",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserMovementLogs_UserKey",
                table: "UserMovementLogs",
                column: "UserKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserAccounts");

            migrationBuilder.DropTable(
                name: "PoiAudioAssets");

            migrationBuilder.DropTable(
                name: "PoiTranslations");

            migrationBuilder.DropTable(
                name: "TourStops");

            migrationBuilder.DropTable(
                name: "UserMovementLogs");

            migrationBuilder.DropTable(
                name: "Tours");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "NarrationHistories");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "NarrationHistories");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "NarrationHistories");
        }
    }
}
