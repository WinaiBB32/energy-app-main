namespace EnergyApp.API.Models
{
    public class SparePartTransaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SparePartId { get; set; }
        public SparePart? SparePart { get; set; }
        public string TxType { get; set; } = string.Empty; // receive, issue, adjust
        public decimal Quantity { get; set; }
        public string ReferenceType { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public string RequestedByUid { get; set; } = string.Empty;
        public string ApprovedByUid { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
