namespace EnergyApp.API.Models
{
    public class SarabanRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DocType { get; set; } = "BOOK"; // BOOK or PERSON
        public string DocNumber { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string FromOrganization { get; set; } = string.Empty;
        public string ToOrganization { get; set; } = string.Empty;
        public DateTime? ReceivedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string Priority { get; set; } = "normal";
        public string Status { get; set; } = "pending";
        public string AssignedTo { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string RecordedBy { get; set; } = string.Empty;
        public Guid? DepartmentId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
