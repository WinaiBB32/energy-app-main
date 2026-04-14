namespace EnergyApp.API.Models
{
    public class SparePartIssueRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string RequestNo { get; set; } = string.Empty;
        public Guid? ServiceRequestId { get; set; }
        public string RequestedByUid { get; set; } = string.Empty;
        public string RequestedByName { get; set; } = string.Empty;
        public string Status { get; set; } = "pending"; // pending, approved, rejected
        public string ApprovedByUid { get; set; } = string.Empty;
        public string ApprovedByName { get; set; } = string.Empty;
        public string RejectReason { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedAt { get; set; }
        public ICollection<SparePartIssueRequestItem> Items { get; set; } = new List<SparePartIssueRequestItem>();
    }
}
