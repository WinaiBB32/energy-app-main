namespace EnergyApp.API.Models
{
    public class SparePartIssueRequestItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SparePartIssueRequestId { get; set; }
        public SparePartIssueRequest? SparePartIssueRequest { get; set; }
        public Guid SparePartId { get; set; }
        public SparePart? SparePart { get; set; }
        public decimal QtyRequested { get; set; }
        public decimal QtyApproved { get; set; }
    }
}
