using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExtensionSuite.Data.Migrations
{
    /// <inheritdoc />
    public partial class ValuationRuleLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "ValuationRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Older schemas cannot represent tombstones; preserve removed rules as absent.
            migrationBuilder.Sql("DELETE FROM ValuationRules WHERE Enabled = 0;");
            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "ValuationRules");
        }
    }
}
