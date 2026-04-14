namespace EnergyApp.API.Models
{
    public class MeetingRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RoomId { get; set; }
        public DateTime RecordMonth { get; set; }
        public int UsageCount { get; set; }
        public string RecordedByUid { get; set; } = string.Empty;
        public string RecordedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
