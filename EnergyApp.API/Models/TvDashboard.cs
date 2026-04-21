namespace EnergyApp.API.Models
{
    public class TvDashboard
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int RefreshIntervalSeconds { get; set; } = 60;
        public int SlideDurationSeconds { get; set; } = 10;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<TvDashboardWidget> Widgets { get; set; } = new List<TvDashboardWidget>();
    }

    public class TvDashboardWidget
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TvDashboardId { get; set; }
        public string WidgetType { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsVisible { get; set; } = true;
        public TvDashboard TvDashboard { get; set; } = null!;
    }
}
