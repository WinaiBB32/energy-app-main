namespace EnergyApp.API.Models
{
    public class ElectricityRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Type { get; set; } = "PEA_BILL"; // PEA_BILL or SOLAR_PRODUCTION
        public string DocReceiveNumber { get; set; } = string.Empty;
        public string DocNumber { get; set; } = string.Empty;
        public Guid? BuildingId { get; set; }
        public DateTime? BillingCycle { get; set; }
        public decimal PeaUnitUsed { get; set; }
        public decimal PeaAmount { get; set; }
        public decimal FtRate { get; set; }
        // Solar fields
        public DateTime? RecordDate { get; set; }
        public decimal SolarUnitProduced { get; set; }
        public string Note { get; set; } = string.Empty;
        public string RecordedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
