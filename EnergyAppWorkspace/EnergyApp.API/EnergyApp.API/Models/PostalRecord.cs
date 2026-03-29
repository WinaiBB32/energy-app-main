namespace EnergyApp.API.Models
{
    public class PostalRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? DepartmentId { get; set; }
        public DateTime RecordMonth { get; set; }
        public int NormalMail { get; set; }
        public int RegisteredMail { get; set; }
        public int EmsMail { get; set; }
        public int TotalMail { get; set; }
        public string RecordedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
