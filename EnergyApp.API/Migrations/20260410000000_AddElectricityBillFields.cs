using System;
using EnergyApp.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyApp.API.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260410000000_AddElectricityBillFields")]
    public partial class AddElectricityBillFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "ElectricityBills",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MeterCode",
                table: "ElectricityBills",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "OnPeakUnits",
                table: "ElectricityBills",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OffPeakUnits",
                table: "ElectricityBills",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FtAmount",
                table: "ElectricityBills",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyServiceFee",
                table: "ElectricityBills",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "InvoiceNumber", table: "ElectricityBills");
            migrationBuilder.DropColumn(name: "MeterCode", table: "ElectricityBills");
            migrationBuilder.DropColumn(name: "OnPeakUnits", table: "ElectricityBills");
            migrationBuilder.DropColumn(name: "OffPeakUnits", table: "ElectricityBills");
            migrationBuilder.DropColumn(name: "FtAmount", table: "ElectricityBills");
            migrationBuilder.DropColumn(name: "MonthlyServiceFee", table: "ElectricityBills");
        }
    }
}
