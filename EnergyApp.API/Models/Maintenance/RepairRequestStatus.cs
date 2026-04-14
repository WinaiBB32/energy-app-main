namespace EnergyApp.API.Models
{
    public static class RepairRequestStatus
    {
        public const string New = "new";
        public const string Assigned = "assigned";
        public const string InProgress = "in_progress";
        public const string NeedSupervisorReview = "need_supervisor_review";
        public const string ReturnedToTechnician = "returned_to_technician";
        public const string WaitingDepartmentExternalProcurement = "waiting_department_external_procurement";
        public const string WaitingCentralExternalProcurement = "waiting_central_external_procurement";
        public const string ExternalScheduled = "external_scheduled";
        public const string ExternalInProgress = "external_in_progress";
        public const string Resolved = "resolved";
        public const string Closed = "closed";
    }
}
