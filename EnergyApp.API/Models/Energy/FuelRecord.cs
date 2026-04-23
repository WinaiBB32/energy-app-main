namespace EnergyApp.API.Models
{
    public class FuelRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? DepartmentId { get; set; }
        public DateTime? RefuelDate { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string VehiclePlate { get; set; } = string.Empty;
        public string VehicleProvince { get; set; } = string.Empty;
        public string PurchaserName { get; set; } = string.Empty;
        public string FuelTypeName { get; set; } = string.Empty;
        public decimal Liters { get; set; }
        public decimal TotalAmount { get; set; }
        public string GasStationCompany { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string RecordedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
