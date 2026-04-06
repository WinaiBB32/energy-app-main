using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyApp.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSolarDetailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ProductionWh",
                table: "ElectricityRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ToBatteryWh",
                table: "ElectricityRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ToGridWh",
                table: "ElectricityRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ToHomeWh",
                table: "ElectricityRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ConsumptionWh",
                table: "ElectricityRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FromBatteryWh",
                table: "ElectricityRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FromGridWh",
                table: "ElectricityRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FromSolarWh",
                table: "ElectricityRecords",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ProductionWh", table: "ElectricityRecords");
            migrationBuilder.DropColumn(name: "ToBatteryWh", table: "ElectricityRecords");
            migrationBuilder.DropColumn(name: "ToGridWh", table: "ElectricityRecords");
            migrationBuilder.DropColumn(name: "ToHomeWh", table: "ElectricityRecords");
            migrationBuilder.DropColumn(name: "ConsumptionWh", table: "ElectricityRecords");
            migrationBuilder.DropColumn(name: "FromBatteryWh", table: "ElectricityRecords");
            migrationBuilder.DropColumn(name: "FromGridWh", table: "ElectricityRecords");
            migrationBuilder.DropColumn(name: "FromSolarWh", table: "ElectricityRecords");
        }
    }
}
