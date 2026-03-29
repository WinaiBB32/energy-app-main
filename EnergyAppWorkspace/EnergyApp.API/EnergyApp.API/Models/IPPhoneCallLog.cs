namespace EnergyApp.API.Models
{
    public class IPPhoneCallLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string StatId { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public DateTime ReportMonth { get; set; }
        public int AnsweredInbound { get; set; }
        public int NoAnswerInbound { get; set; }
        public int BusyInbound { get; set; }
        public int FailedInbound { get; set; }
        public int VoicemailInbound { get; set; }
        public int TotalInbound { get; set; }
        public int AnsweredOutbound { get; set; }
        public int NoAnswerOutbound { get; set; }
        public int BusyOutbound { get; set; }
        public int FailedOutbound { get; set; }
        public int VoicemailOutbound { get; set; }
        public int TotalOutbound { get; set; }
        public int TotalCalls { get; set; }
        public string TotalTalkDuration { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
