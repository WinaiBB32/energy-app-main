namespace EnergyApp.API.Models
{
    public class FuelType
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Severity { get; set; } = "secondary";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
