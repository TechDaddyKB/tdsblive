using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExtensionSuite.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinancialMetadataBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve typed facts from pre-correlation rows without changing accepted accounting values.
            migrationBuilder.Sql("""
                WITH facts AS (
                    SELECT Id, json_extract(SafeJson, '$.Tier') AS Tier,
                        json_extract(SafeJson, '$.GiftRole') AS Role,
                        json_extract(SafeJson, '$.GiftScopeKey') AS Scope,
                        json_extract(SafeJson, '$.userPlatformId') AS Sender
                    FROM (SELECT Id, CASE WHEN json_valid(MetadataJson) THEN MetadataJson ELSE '{}' END AS SafeJson FROM FinancialEvents)
                )
                UPDATE FinancialEvents SET
                    GiftTier = CASE WHEN GiftTier = '' AND typeof(facts.Tier) = 'text' AND length(facts.Tier) <= 64 THEN facts.Tier ELSE GiftTier END,
                    GiftRole = CASE WHEN GiftRole = 'none' AND facts.Role IN ('batch', 'individual', 'standalone', 'recipient') THEN facts.Role ELSE GiftRole END,
                    GiftScopeKey = CASE WHEN GiftScopeKey IS NULL AND typeof(facts.Scope) = 'text' AND length(facts.Scope) BETWEEN 1 AND 256 THEN facts.Scope ELSE GiftScopeKey END,
                    GiftSenderKey = CASE WHEN GiftSenderKey IS NULL AND typeof(facts.Sender) = 'text' AND length(facts.Sender) BETWEEN 1 AND 509 THEN 'id:' || facts.Sender ELSE GiftSenderKey END
                FROM facts WHERE facts.Id = FinancialEvents.Id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Clearing derived fields would erase legitimate evidence belonging to newer records.
        }
    }
}
