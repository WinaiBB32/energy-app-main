using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyApp.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingRepairAndSparePartWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminOfficerName",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdminOfficerUid",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AssetNumber",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BuildingName",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "ServiceRequests",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedByName",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClosedByUid",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EscalationReason",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalCompletedAt",
                table: "ServiceRequests",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalResult",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalScheduledAt",
                table: "ServiceRequests",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalVendorName",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsCentralAsset",
                table: "ServiceRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LocationDetail",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequesterDepartmentCode",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequesterDepartmentName",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "SupervisorCanRepairInHouse",
                table: "ServiceRequests",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupervisorExternalAdvice",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SupervisorName",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SupervisorReason",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SupervisorRepairPlan",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SupervisorUid",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TechnicianAction",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TechnicianDiagnosis",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TechnicianName",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TechnicianUid",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkOrderNo",
                table: "ServiceRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ElectricityBills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocReceiveNumber = table.Column<string>(type: "text", nullable: false),
                    DocNumber = table.Column<string>(type: "text", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: true),
                    BillingCycle = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PeaUnitUsed = table.Column<decimal>(type: "numeric", nullable: false),
                    PeaAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    FtRate = table.Column<decimal>(type: "numeric", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectricityBills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolarProductions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SolarUnitProduced = table.Column<decimal>(type: "numeric", nullable: false),
                    ProductionWh = table.Column<decimal>(type: "numeric", nullable: false),
                    ToBatteryWh = table.Column<decimal>(type: "numeric", nullable: false),
                    ToGridWh = table.Column<decimal>(type: "numeric", nullable: false),
                    ToHomeWh = table.Column<decimal>(type: "numeric", nullable: false),
                    ConsumptionWh = table.Column<decimal>(type: "numeric", nullable: false),
                    FromBatteryWh = table.Column<decimal>(type: "numeric", nullable: false),
                    FromGridWh = table.Column<decimal>(type: "numeric", nullable: false),
                    FromSolarWh = table.Column<decimal>(type: "numeric", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolarProductions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SparePartIssueRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestNo = table.Column<string>(type: "text", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByUid = table.Column<string>(type: "text", nullable: false),
                    RequestedByName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ApprovedByUid = table.Column<string>(type: "text", nullable: false),
                    ApprovedByName = table.Column<string>(type: "text", nullable: false),
                    RejectReason = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SparePartIssueRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpareParts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartCode = table.Column<string>(type: "text", nullable: false),
                    PartName = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric", nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpareParts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SparePartIssueRequestItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SparePartIssueRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SparePartId = table.Column<Guid>(type: "uuid", nullable: false),
                    QtyRequested = table.Column<decimal>(type: "numeric", nullable: false),
                    QtyApproved = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SparePartIssueRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SparePartIssueRequestItems_SparePartIssueRequests_SparePart~",
                        column: x => x.SparePartIssueRequestId,
                        principalTable: "SparePartIssueRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SparePartIssueRequestItems_SpareParts_SparePartId",
                        column: x => x.SparePartId,
                        principalTable: "SpareParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SparePartTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SparePartId = table.Column<Guid>(type: "uuid", nullable: false),
                    TxType = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    ReferenceType = table.Column<string>(type: "text", nullable: false),
                    ReferenceId = table.Column<string>(type: "text", nullable: false),
                    RequestedByUid = table.Column<string>(type: "text", nullable: false),
                    ApprovedByUid = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SparePartTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SparePartTransactions_SpareParts_SparePartId",
                        column: x => x.SparePartId,
                        principalTable: "SpareParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SparePartIssueRequestItems_SparePartId",
                table: "SparePartIssueRequestItems",
                column: "SparePartId");

            migrationBuilder.CreateIndex(
                name: "IX_SparePartIssueRequestItems_SparePartIssueRequestId",
                table: "SparePartIssueRequestItems",
                column: "SparePartIssueRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SparePartTransactions_SparePartId",
                table: "SparePartTransactions",
                column: "SparePartId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElectricityBills");

            migrationBuilder.DropTable(
                name: "SolarProductions");

            migrationBuilder.DropTable(
                name: "SparePartIssueRequestItems");

            migrationBuilder.DropTable(
                name: "SparePartTransactions");

            migrationBuilder.DropTable(
                name: "SparePartIssueRequests");

            migrationBuilder.DropTable(
                name: "SpareParts");

            migrationBuilder.DropColumn(
                name: "AdminOfficerName",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "AdminOfficerUid",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "AssetNumber",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "BuildingName",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ClosedByName",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ClosedByUid",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "EscalationReason",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ExternalCompletedAt",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ExternalResult",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ExternalScheduledAt",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ExternalVendorName",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "IsCentralAsset",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "LocationDetail",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "RequesterDepartmentCode",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "RequesterDepartmentName",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "SupervisorCanRepairInHouse",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "SupervisorExternalAdvice",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "SupervisorName",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "SupervisorReason",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "SupervisorRepairPlan",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "SupervisorUid",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "TechnicianAction",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "TechnicianDiagnosis",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "TechnicianName",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "TechnicianUid",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "WorkOrderNo",
                table: "ServiceRequests");
        }
    }
}
