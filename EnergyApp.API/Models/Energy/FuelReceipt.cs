using System.Text.Json;

namespace EnergyApp.API.Models
{
    public class FuelReceipt
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? DepartmentId { get; set; }
        public string EntriesJson { get; set; } = "[]"; // JSON array of receipt entries
        public decimal TotalAmount { get; set; }
        public string DeclarerName { get; set; } = string.Empty;
        public string DeclarerPosition { get; set; } = string.Empty;
        public string DeclarerDept { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string RecordedByName { get; set; } = string.Empty;
        public string RecordedByUid { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
