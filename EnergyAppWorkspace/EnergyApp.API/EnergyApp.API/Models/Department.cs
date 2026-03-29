namespace EnergyApp.API.Models
{
    public class Department
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        // ความสัมพันธ์: 1 แผนก มีพนักงานได้หลายคน
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}