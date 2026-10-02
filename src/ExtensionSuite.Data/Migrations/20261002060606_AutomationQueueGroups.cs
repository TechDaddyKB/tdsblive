using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExtensionSuite.Data.Migrations
{
    /// <inheritdoc />
    public partial class AutomationQueueGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActionOrder",
                table: "AutomationExecutions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "QueueGroup",
                table: "AutomationExecutions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_QueueGroup_State",
                table: "AutomationExecutions",
                columns: new[] { "QueueGroup", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AutomationExecutions_QueueGroup_State",
                table: "AutomationExecutions");

            migrationBuilder.DropColumn(
                name: "ActionOrder",
                table: "AutomationExecutions");

            migrationBuilder.DropColumn(
                name: "QueueGroup",
                table: "AutomationExecutions");
        }
    }
}
