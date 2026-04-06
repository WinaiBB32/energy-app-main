namespace EnergyApp.API
{
    public static class Roles
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string Admin = "admin";
        public const string User = "User";
        public const string Technician = "technician";
        public const string Supervisor = "supervisor";
        public const string AdminBuilding = "adminbuilding";
    }

    public static class UserStatus
    {
        public const string Pending = "pending";
        public const string Active = "active";
        public const string Approved = "approved";
        public const string Rejected = "rejected";
    }
}
