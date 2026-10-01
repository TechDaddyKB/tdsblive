using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExtensionSuite.Data.Migrations
{
    /// <inheritdoc />
    public partial class OverlayRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Json = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Overlays",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Json = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Overlays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OverlayTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OverlayId = table.Column<string>(type: "TEXT", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAtTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    Revoked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OverlayTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OverlayTokens_Overlays_OverlayId",
                        column: x => x.OverlayId,
                        principalTable: "Overlays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OverlayTokens_Hash",
                table: "OverlayTokens",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OverlayTokens_OverlayId",
                table: "OverlayTokens",
                column: "OverlayId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "OverlayTokens");

            migrationBuilder.DropTable(
                name: "Overlays");
        }
    }
}
