using EnergyApp.API.Models;
using EnergyApp.API.Models.Office;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Building> Buildings { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<FuelType> FuelTypes { get; set; }
        public DbSet<MeetingRoom> MeetingRooms { get; set; }
        public DbSet<ElectricityRecord> ElectricityRecord { get; set; }
        public DbSet<ElectricityBill> ElectricityBills { get; set; }
        public DbSet<SolarProduction> SolarProductions { get; set; }
        public DbSet<WaterRecord> WaterRecords { get; set; }
        public DbSet<FuelRecord> FuelRecords { get; set; }
        public DbSet<FuelReceipt> FuelReceipts { get; set; }
        public DbSet<TelephoneRecord> TelephoneRecords { get; set; }
        public DbSet<IPPhoneDirectory> IPPhoneDirectories { get; set; }
        public DbSet<ServiceRequest> ServiceRequests { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<PostalRecord> PostalRecords { get; set; }
        public DbSet<MeetingRecord> MeetingRecords { get; set; }
        public DbSet<IPPhoneMonthStat> IPPhoneMonthStats { get; set; }
        public DbSet<IPPhoneCallLog> IPPhoneCallLogs { get; set; }
        public DbSet<SparePart> SpareParts { get; set; }
        public DbSet<SparePartTransaction> SparePartTransactions { get; set; }
        public DbSet<SparePartIssueRequest> SparePartIssueRequests { get; set; }
        public DbSet<SparePartIssueRequestItem> SparePartIssueRequestItems { get; set; }
        public DbSet<VehicleRecord> VehicleRecords { get; set; }
        public DbSet<VehicleDepartment> VehicleDepartments { get; set; }
        public DbSet<VehicleProvince> VehicleProvinces { get; set; }
        public DbSet<SarabanStat> SarabanStats { get; set; }
        public DbSet<TvDashboard> TvDashboards { get; set; }
        public DbSet<TvDashboardWidget> TvDashboardWidgets { get; set; }
    }
}
