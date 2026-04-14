namespace EnergyApp.API.Models
{
    public class ServiceRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string WorkOrderNo { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = "medium";
        public string Status { get; set; } = RepairRequestStatus.New;
        public string BuildingName { get; set; } = string.Empty;
        public string LocationDetail { get; set; } = string.Empty;
        public string AssetNumber { get; set; } = string.Empty;
        public bool IsCentralAsset { get; set; }
        public string Extension { get; set; } = string.Empty;
        public string RequesterName { get; set; } = string.Empty;
        public string RequesterEmail { get; set; } = string.Empty;
        public string RequesterUid { get; set; } = string.Empty;
        public string RequesterDepartmentCode { get; set; } = string.Empty;
        public string RequesterDepartmentName { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public string TechnicianUid { get; set; } = string.Empty;
        public string TechnicianName { get; set; } = string.Empty;
        public string SupervisorUid { get; set; } = string.Empty;
        public string SupervisorName { get; set; } = string.Empty;
        public string AdminOfficerUid { get; set; } = string.Empty;
        public string AdminOfficerName { get; set; } = string.Empty;
        public string TechnicianDiagnosis { get; set; } = string.Empty;
        public string TechnicianAction { get; set; } = string.Empty;
        public string EscalationReason { get; set; } = string.Empty;
        public bool? SupervisorCanRepairInHouse { get; set; }
        public string SupervisorReason { get; set; } = string.Empty;
        public string SupervisorRepairPlan { get; set; } = string.Empty;
        public string SupervisorExternalAdvice { get; set; } = string.Empty;
        public string ExternalVendorName { get; set; } = string.Empty;
        public DateTime? ExternalScheduledAt { get; set; }
        public DateTime? ExternalCompletedAt { get; set; }
        public string ExternalResult { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string ClosedByUid { get; set; } = string.Empty;
        public string ClosedByName { get; set; } = string.Empty;
        public DateTime? ClosedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
