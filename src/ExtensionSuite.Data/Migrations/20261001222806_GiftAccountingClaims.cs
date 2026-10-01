using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExtensionSuite.Data.Migrations
{
    /// <inheritdoc />
    public partial class GiftAccountingClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GiftRole",
                table: "FinancialEvents",
                type: "TEXT",
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<string>(
                name: "GiftScopeKey",
                table: "FinancialEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GiftSenderKey",
                table: "FinancialEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GiftTier",
                table: "FinancialEvents",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "GiftAccountingClaims",
                columns: table => new
                {
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerContributionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Tier = table.Column<string>(type: "TEXT", nullable: false),
                    SenderKey = table.Column<string>(type: "TEXT", nullable: true),
                    Quantity = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftAccountingClaims", x => new { x.Platform, x.KeyHash });
                    table.ForeignKey(
                        name: "FK_GiftAccountingClaims_FinancialEvents_OwnerContributionId",
                        column: x => x.OwnerContributionId,
                        principalTable: "FinancialEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvents_Platform_GiftScopeKey_GiftCorrelationKey_GiftRole",
                table: "FinancialEvents",
                columns: new[] { "Platform", "GiftScopeKey", "GiftCorrelationKey", "GiftRole" });

            migrationBuilder.CreateIndex(
                name: "IX_GiftAccountingClaims_OwnerContributionId",
                table: "GiftAccountingClaims",
                column: "OwnerContributionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GiftAccountingClaims");

            migrationBuilder.DropIndex(
                name: "IX_FinancialEvents_Platform_GiftScopeKey_GiftCorrelationKey_GiftRole",
                table: "FinancialEvents");

            migrationBuilder.DropColumn(
                name: "GiftRole",
                table: "FinancialEvents");

            migrationBuilder.DropColumn(
                name: "GiftScopeKey",
                table: "FinancialEvents");

            migrationBuilder.DropColumn(
                name: "GiftSenderKey",
                table: "FinancialEvents");

            migrationBuilder.DropColumn(
                name: "GiftTier",
                table: "FinancialEvents");
        }
    }
}
