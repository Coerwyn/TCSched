using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCSched.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EventMinPlayersSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinPlayers",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            // Backfill existing events from their template (clamped to capacity) before the
            // check constraint is added; the generated default of 0 would violate it.
            migrationBuilder.Sql(
                """
                UPDATE "Events"
                SET "MinPlayers" = MIN("Capacity",
                    (SELECT "MinPlayers" FROM "GameTemplates" WHERE "GameTemplates"."Id" = "Events"."GameTemplateId"));
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Events_MinPlayers",
                table: "Events",
                sql: "\"MinPlayers\" >= 1 AND \"MinPlayers\" <= \"Capacity\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Events_MinPlayers",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "MinPlayers",
                table: "Events");
        }
    }
}
