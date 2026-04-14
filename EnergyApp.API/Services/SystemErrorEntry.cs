namespace EnergyApp.API.Services
{
    public class SystemErrorEntry
    {
        public DateTime OccurredAtUtc { get; set; }
        public string Method { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ExceptionType { get; set; }
        public string TraceId { get; set; } = string.Empty;
    }
}
