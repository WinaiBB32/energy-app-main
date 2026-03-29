namespace EnergyApp.API.Models
{
    public class WaterRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DocReceiveNumber { get; set; } = string.Empty;
        public string DocNumber { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime? BillingCycle { get; set; }
        public string RegistrationNo { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UsageAddress { get; set; } = string.Empty;
        public DateTime? ReadingDate { get; set; }
        public decimal CurrentMeter { get; set; }
        public decimal WaterUnitUsed { get; set; }
        public decimal RawWaterCharge { get; set; }
        public decimal WaterAmount { get; set; }
        public decimal MonthlyServiceFee { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string RecordedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
