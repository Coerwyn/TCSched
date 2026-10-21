using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCSched.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameTemplates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Formats = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultFormat = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultDurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultCapacity = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxCapacity = table.Column<int>(type: "INTEGER", nullable: false),
                    MinPlayers = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GameTemplateId = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisteredCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.CheckConstraint("CK_Events_Capacity", "\"Capacity\" BETWEEN 1 AND 30");
                    table.CheckConstraint("CK_Events_RegisteredCount", "\"RegisteredCount\" >= 0 AND \"RegisteredCount\" <= \"Capacity\"");
                    table.ForeignKey(
                        name: "FK_Events_GameTemplates_GameTemplateId",
                        column: x => x.GameTemplateId,
                        principalTable: "GameTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Registrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Registrations_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_GameTemplateId",
                table: "Events",
                column: "GameTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_StartsAtUtc",
                table: "Events",
                column: "StartsAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_EventId_NormalizedName",
                table: "Registrations",
                columns: new[] { "EventId", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Registrations");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "GameTemplates");
        }
    }
}
