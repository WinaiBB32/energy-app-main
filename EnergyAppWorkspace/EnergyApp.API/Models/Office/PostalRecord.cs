namespace EnergyApp.API.Models
{
    public class PostalRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? DepartmentId { get; set; }
        public DateTime RecordMonth { get; set; }
        public int IncomingNormalMail { get; set; }
        public int IncomingRegisteredMail { get; set; }
        public int IncomingEmsMail { get; set; }
        public int IncomingTotalMail { get; set; }
        public int NormalMail { get; set; }
        public decimal NormalMailUnitPrice { get; set; }
        public int RegisteredMail { get; set; }
        public decimal RegisteredMailUnitPrice { get; set; }
        public int EmsMail { get; set; }
        public decimal EmsMailUnitPrice { get; set; }
        public int TotalMail { get; set; }
        public string RecordedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
