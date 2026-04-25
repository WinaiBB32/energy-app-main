using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyApp.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTvDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TvDashboards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RefreshIntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    SlideDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvDashboards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TvDashboardWidgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TvDashboardId = table.Column<Guid>(type: "uuid", nullable: false),
                    WidgetType = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvDashboardWidgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TvDashboardWidgets_TvDashboards_TvDashboardId",
                        column: x => x.TvDashboardId,
                        principalTable: "TvDashboards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TvDashboardWidgets_TvDashboardId",
                table: "TvDashboardWidgets",
                column: "TvDashboardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TvDashboardWidgets");
            migrationBuilder.DropTable(name: "TvDashboards");
        }
    }
}
