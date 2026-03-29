using EnergyApp.API.Models;
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
    }
}
