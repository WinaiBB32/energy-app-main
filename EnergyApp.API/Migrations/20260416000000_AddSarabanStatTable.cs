using System;
using EnergyApp.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyApp.API.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260416000000_AddSarabanStatTable")]
    /// <inheritdoc />
    public partial class AddSarabanStatTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SarabanStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookType = table.Column<string>(type: "text", nullable: false),
                    BookName = table.Column<string>(type: "text", nullable: false),
                    RecordMonth = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceiverName = table.Column<string>(type: "text", nullable: false),
                    ReceivedCount = table.Column<int>(type: "integer", nullable: false),
                    InternalPaperCount = table.Column<int>(type: "integer", nullable: false),
                    InternalDigitalCount = table.Column<int>(type: "integer", nullable: false),
                    ExternalPaperCount = table.Column<int>(type: "integer", nullable: false),
                    ExternalDigitalCount = table.Column<int>(type: "integer", nullable: false),
                    ForwardedCount = table.Column<int>(type: "integer", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SarabanStats", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SarabanStats");
        }
    }
}
