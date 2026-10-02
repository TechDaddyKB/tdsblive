using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExtensionSuite.Data.Migrations
{
    /// <inheritdoc />
    public partial class AutomationExecutionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Detail",
                table: "AutomationExecutions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Detail",
                table: "AutomationExecutions");
        }
    }
}
