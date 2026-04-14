namespace EnergyApp.API.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
        public string Status { get; set; } = "pending";
        public bool IsActive { get; set; } = true;

        public Guid? DepartmentId { get; set; }
        public Department? Department { get; set; }

        // --- เพิ่มใหม่: เก็บสิทธิ์การเข้าถึงรายระบบ ---
        // Npgsql (PostgreSQL) จะแปลง List<string> เป็น Text Array (text[]) ให้อัตโนมัติ!
        public List<string> AccessibleSystems { get; set; } = new List<string>();
        public List<string> AdminSystems { get; set; } = new List<string>();
    }
}
