namespace EnergyApp.API.Models
{
    public class SparePart
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string PartCode { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
        public string Unit { get; set; } = "pcs";
        public decimal QuantityOnHand { get; set; }
        public decimal ReorderPoint { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
