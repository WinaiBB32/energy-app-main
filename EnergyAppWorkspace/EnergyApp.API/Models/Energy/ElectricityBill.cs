namespace EnergyApp.API.Models
{
    public class ElectricityBill
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DocReceiveNumber { get; set; } = string.Empty;
        public string DocNumber { get; set; } = string.Empty;
        public Guid? BuildingId { get; set; }
        public DateTime? BillingCycle { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string MeterCode { get; set; } = string.Empty;
        public decimal OnPeakUnits { get; set; }
        public decimal OffPeakUnits { get; set; }
        public decimal PeaUnitUsed { get; set; }
        public decimal PeaAmount { get; set; }
        public decimal FtRate { get; set; }
        public decimal FtAmount { get; set; }
        public decimal MonthlyServiceFee { get; set; }
        public string Note { get; set; } = string.Empty;
        public string RecordedBy { get; set; } = string.Empty;
        public string DepartmentId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
