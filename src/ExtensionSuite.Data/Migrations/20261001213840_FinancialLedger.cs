using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExtensionSuite.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinancialLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContributionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    Operation = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeJson = table.Column<string>(type: "TEXT", nullable: false),
                    AfterJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FxRates",
                columns: table => new
                {
                    Currency = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedDay = table.Column<int>(type: "INTEGER", nullable: false),
                    Origin = table.Column<string>(type: "TEXT", nullable: false),
                    RateDay = table.Column<int>(type: "INTEGER", nullable: false),
                    UsdPerNativeUnit = table.Column<string>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    Estimated = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FxRates", x => new { x.Currency, x.RequestedDay, x.Origin });
                });

            migrationBuilder.CreateTable(
                name: "Supporters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Supporters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ValuationRules",
                columns: table => new
                {
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Tier = table.Column<string>(type: "TEXT", nullable: false),
                    UsdMinorPerUnit = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValuationRules", x => new { x.Platform, x.Type, x.Tier });
                });

            migrationBuilder.CreateTable(
                name: "SupporterIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SupporterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    IdentityKey = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupporterIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupporterIdentities_Supporters_SupporterId",
                        column: x => x.SupporterId,
                        principalTable: "Supporters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SupporterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IdentityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    NativeEventId = table.Column<string>(type: "TEXT", nullable: true),
                    DedupeKey = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAtTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    NativeAmountMinor = table.Column<long>(type: "INTEGER", nullable: true),
                    NativeCurrency = table.Column<string>(type: "TEXT", nullable: true),
                    NativeMinorUnitDigits = table.Column<int>(type: "INTEGER", nullable: true),
                    UsdAmountMinor = table.Column<long>(type: "INTEGER", nullable: true),
                    ValuationMethod = table.Column<string>(type: "TEXT", nullable: false),
                    Estimated = table.Column<bool>(type: "INTEGER", nullable: false),
                    FxRate = table.Column<string>(type: "TEXT", nullable: true),
                    FxRateDay = table.Column<int>(type: "INTEGER", nullable: true),
                    FxProvider = table.Column<string>(type: "TEXT", nullable: true),
                    PendingReason = table.Column<string>(type: "TEXT", nullable: true),
                    StreamId = table.Column<string>(type: "TEXT", nullable: true),
                    GiftCorrelationKey = table.Column<string>(type: "TEXT", nullable: true),
                    AccountingState = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialEvents_SupporterIdentities_IdentityId",
                        column: x => x.IdentityId,
                        principalTable: "SupporterIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialEvents_Supporters_SupporterId",
                        column: x => x.SupporterId,
                        principalTable: "Supporters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAudits_ContributionId",
                table: "FinancialAudits",
                column: "ContributionId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAudits_CreatedAtTicks",
                table: "FinancialAudits",
                column: "CreatedAtTicks");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvents_EventId",
                table: "FinancialEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvents_IdentityId",
                table: "FinancialEvents",
                column: "IdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvents_OccurredAtTicks",
                table: "FinancialEvents",
                column: "OccurredAtTicks");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvents_Platform",
                table: "FinancialEvents",
                column: "Platform");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvents_Platform_DedupeKey",
                table: "FinancialEvents",
                columns: new[] { "Platform", "DedupeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvents_Platform_NativeEventId",
                table: "FinancialEvents",
                columns: new[] { "Platform", "NativeEventId" },
                unique: true,
                filter: "NativeEventId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvents_StreamId_OccurredAtTicks",
                table: "FinancialEvents",
                columns: new[] { "StreamId", "OccurredAtTicks" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEvents_SupporterId",
                table: "FinancialEvents",
                column: "SupporterId");

            migrationBuilder.CreateIndex(
                name: "IX_SupporterIdentities_Platform_IdentityKey",
                table: "SupporterIdentities",
                columns: new[] { "Platform", "IdentityKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupporterIdentities_SupporterId",
                table: "SupporterIdentities",
                column: "SupporterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialAudits");

            migrationBuilder.DropTable(
                name: "FinancialEvents");

            migrationBuilder.DropTable(
                name: "FxRates");

            migrationBuilder.DropTable(
                name: "ValuationRules");

            migrationBuilder.DropTable(
                name: "SupporterIdentities");

            migrationBuilder.DropTable(
                name: "Supporters");
        }
    }
}
