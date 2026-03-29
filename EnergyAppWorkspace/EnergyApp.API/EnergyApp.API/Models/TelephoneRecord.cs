namespace EnergyApp.API.Models
{
    public class TelephoneRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DocReceiveNumber { get; set; } = string.Empty;
        public string DocNumber { get; set; } = string.Empty;
        public DateTime? BillingCycle { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public decimal UsageAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string RecordedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
