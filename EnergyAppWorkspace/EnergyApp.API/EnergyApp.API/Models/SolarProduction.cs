namespace EnergyApp.API.Models
{
    public class SolarProduction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? BuildingId { get; set; }
        public DateTime? RecordDate { get; set; }
        public decimal SolarUnitProduced { get; set; }
        public decimal ProductionWh { get; set; }
        public decimal ToBatteryWh { get; set; }
        public decimal ToGridWh { get; set; }
        public decimal ToHomeWh { get; set; }
        public decimal ConsumptionWh { get; set; }
        public decimal FromBatteryWh { get; set; }
        public decimal FromGridWh { get; set; }
        public decimal FromSolarWh { get; set; }
        public string Note { get; set; } = string.Empty;
        public string RecordedBy { get; set; } = string.Empty;
        public string DepartmentId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
