namespace EnergyApp.API.Models
{
    public class IPPhoneMonthStat
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime ReportMonth { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;
        public int TotalRecords { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
