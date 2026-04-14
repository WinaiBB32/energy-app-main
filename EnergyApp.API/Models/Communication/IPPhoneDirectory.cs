namespace EnergyApp.API.Models
{
    public class IPPhoneDirectory
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string OwnerName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string IpPhoneNumber { get; set; } = string.Empty;
        public string AnalogNumber { get; set; } = string.Empty;
        public string DeviceCode { get; set; } = string.Empty;
        public Guid? DepartmentId { get; set; }
        public string Workgroup { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
        public bool IsPublished { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
