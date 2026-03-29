namespace EnergyApp.API.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
        public bool IsActive { get; set; } = true;

        public Guid? DepartmentId { get; set; }
        public Department? Department { get; set; }
        public string PasswordHash { get; internal set; }
    }
}
