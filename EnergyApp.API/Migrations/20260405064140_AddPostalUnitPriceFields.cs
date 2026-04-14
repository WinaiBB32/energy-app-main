using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyApp.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPostalUnitPriceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EmsMailUnitPrice",
                table: "PostalRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NormalMailUnitPrice",
                table: "PostalRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegisteredMailUnitPrice",
                table: "PostalRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmsMailUnitPrice",
                table: "PostalRecords");

            migrationBuilder.DropColumn(
                name: "NormalMailUnitPrice",
                table: "PostalRecords");

            migrationBuilder.DropColumn(
                name: "RegisteredMailUnitPrice",
                table: "PostalRecords");
        }
    }
}
