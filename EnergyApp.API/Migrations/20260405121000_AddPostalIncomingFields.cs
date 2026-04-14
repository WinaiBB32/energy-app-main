using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyApp.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPostalIncomingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IncomingEmsMail",
                table: "PostalRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IncomingNormalMail",
                table: "PostalRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IncomingRegisteredMail",
                table: "PostalRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IncomingTotalMail",
                table: "PostalRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncomingEmsMail",
                table: "PostalRecords");

            migrationBuilder.DropColumn(
                name: "IncomingNormalMail",
                table: "PostalRecords");

            migrationBuilder.DropColumn(
                name: "IncomingRegisteredMail",
                table: "PostalRecords");

            migrationBuilder.DropColumn(
                name: "IncomingTotalMail",
                table: "PostalRecords");
        }
    }
}
