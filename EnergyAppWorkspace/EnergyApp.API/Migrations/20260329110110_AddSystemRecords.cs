using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyApp.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    SenderName = table.Column<string>(type: "text", nullable: false),
                    SenderEmail = table.Column<string>(type: "text", nullable: false),
                    SenderId = table.Column<string>(type: "text", nullable: false),
                    SenderRole = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ElectricityRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    DocReceiveNumber = table.Column<string>(type: "text", nullable: false),
                    DocNumber = table.Column<string>(type: "text", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: true),
                    BillingCycle = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PeaUnitUsed = table.Column<decimal>(type: "numeric", nullable: false),
                    PeaAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    FtRate = table.Column<decimal>(type: "numeric", nullable: false),
                    RecordDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SolarUnitProduced = table.Column<decimal>(type: "numeric", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectricityRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FuelReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntriesJson = table.Column<string>(type: "text", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    DeclarerName = table.Column<string>(type: "text", nullable: false),
                    DeclarerPosition = table.Column<string>(type: "text", nullable: false),
                    DeclarerDept = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    RecordedByName = table.Column<string>(type: "text", nullable: false),
                    RecordedByUid = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuelReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FuelRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefuelDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    DocumentNumber = table.Column<string>(type: "text", nullable: false),
                    VehiclePlate = table.Column<string>(type: "text", nullable: false),
                    VehicleProvince = table.Column<string>(type: "text", nullable: false),
                    PurchaserName = table.Column<string>(type: "text", nullable: false),
                    FuelTypeName = table.Column<string>(type: "text", nullable: false),
                    Liters = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    GasStationCompany = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuelRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IPPhoneDirectories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerName = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    IpPhoneNumber = table.Column<string>(type: "text", nullable: false),
                    AnalogNumber = table.Column<string>(type: "text", nullable: false),
                    DeviceCode = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Workgroup = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Keywords = table.Column<string>(type: "text", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IPPhoneDirectories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeetingRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordMonth = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    RecordedByUid = table.Column<string>(type: "text", nullable: false),
                    RecordedByName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostalRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordMonth = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NormalMail = table.Column<int>(type: "integer", nullable: false),
                    RegisteredMail = table.Column<int>(type: "integer", nullable: false),
                    EmsMail = table.Column<int>(type: "integer", nullable: false),
                    TotalMail = table.Column<int>(type: "integer", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostalRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SarabanRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocType = table.Column<string>(type: "text", nullable: false),
                    DocNumber = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    FromOrganization = table.Column<string>(type: "text", nullable: false),
                    ToOrganization = table.Column<string>(type: "text", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AssignedTo = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SarabanRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Extension = table.Column<string>(type: "text", nullable: false),
                    RequesterName = table.Column<string>(type: "text", nullable: false),
                    RequesterEmail = table.Column<string>(type: "text", nullable: false),
                    RequesterUid = table.Column<string>(type: "text", nullable: false),
                    AssignedTo = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelephoneRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocReceiveNumber = table.Column<string>(type: "text", nullable: false),
                    DocNumber = table.Column<string>(type: "text", nullable: false),
                    BillingCycle = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    ProviderName = table.Column<string>(type: "text", nullable: false),
                    UsageAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelephoneRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaterRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocReceiveNumber = table.Column<string>(type: "text", nullable: false),
                    DocNumber = table.Column<string>(type: "text", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: false),
                    BillingCycle = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegistrationNo = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: false),
                    UsageAddress = table.Column<string>(type: "text", nullable: false),
                    ReadingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentMeter = table.Column<decimal>(type: "numeric", nullable: false),
                    WaterUnitUsed = table.Column<decimal>(type: "numeric", nullable: false),
                    RawWaterCharge = table.Column<decimal>(type: "numeric", nullable: false),
                    WaterAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    MonthlyServiceFee = table.Column<decimal>(type: "numeric", nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterRecords", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ElectricityRecords");

            migrationBuilder.DropTable(
                name: "FuelReceipts");

            migrationBuilder.DropTable(
                name: "FuelRecords");

            migrationBuilder.DropTable(
                name: "IPPhoneDirectories");

            migrationBuilder.DropTable(
                name: "MeetingRecords");

            migrationBuilder.DropTable(
                name: "PostalRecords");

            migrationBuilder.DropTable(
                name: "SarabanRecords");

            migrationBuilder.DropTable(
                name: "ServiceRequests");

            migrationBuilder.DropTable(
                name: "TelephoneRecords");

            migrationBuilder.DropTable(
                name: "WaterRecords");
        }
    }
}
