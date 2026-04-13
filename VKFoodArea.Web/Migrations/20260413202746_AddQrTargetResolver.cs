using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VKFoodArea.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddQrTargetResolver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "QrCodeItems",
                newName: "QrCodeItems_Legacy");

            migrationBuilder.CreateTable(
                name: "QrCodeItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TargetId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrCodeItems", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO QrCodeItems (Id, Code, Title, TargetType, TargetId, IsActive, CreatedAt)
                SELECT Id, Code, Title, 'poi', PoiId, IsActive, CreatedAt
                FROM QrCodeItems_Legacy;
                """);

            migrationBuilder.DropTable(
                name: "QrCodeItems_Legacy");

            migrationBuilder.CreateIndex(
                name: "IX_QrCodeItems_Code",
                table: "QrCodeItems",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QrCodeItems_TargetType_TargetId",
                table: "QrCodeItems",
                columns: new[] { "TargetType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "QrCodeItems",
                newName: "QrCodeItems_Targeted");

            migrationBuilder.CreateTable(
                name: "QrCodeItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    PoiId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrCodeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QrCodeItems_Pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "Pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO QrCodeItems (Id, Code, Title, PoiId, IsActive, CreatedAt)
                SELECT Id, Code, Title, TargetId, IsActive, CreatedAt
                FROM QrCodeItems_Targeted
                WHERE lower(TargetType) = 'poi';
                """);

            migrationBuilder.DropTable(
                name: "QrCodeItems_Targeted");

            migrationBuilder.CreateIndex(
                name: "IX_QrCodeItems_PoiId",
                table: "QrCodeItems",
                column: "PoiId");
        }
    }
}
