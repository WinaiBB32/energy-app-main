namespace EnergyApp.API.Models
{
    public class Building
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty ;
        public bool IsActive { get; set; } = true;
    }
}
