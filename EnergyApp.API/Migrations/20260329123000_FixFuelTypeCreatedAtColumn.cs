using EnergyApp.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyApp.API.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260329123000_FixFuelTypeCreatedAtColumn")]
    public partial class FixFuelTypeCreatedAtColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name = 'FuelTypes'
      AND column_name = 'CreatedkAt'
  ) AND NOT EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name = 'FuelTypes'
      AND column_name = 'CreatedAt'
  ) THEN
    ALTER TABLE ""FuelTypes"" RENAME COLUMN ""CreatedkAt"" TO ""CreatedAt"";
  END IF;

  IF NOT EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name = 'FuelTypes'
      AND column_name = 'CreatedAt'
  ) THEN
    ALTER TABLE ""FuelTypes"" ADD COLUMN ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW();
  END IF;
END $$;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
