namespace EnergyApp.API.Services
{
    public class InMemorySystemErrorLogStore : ISystemErrorLogStore
    {
        private readonly List<SystemErrorEntry> _entries = new();
        private readonly object _sync = new();
        private const int MaxEntries = 500;

        public void Add(SystemErrorEntry entry)
        {
            lock (_sync)
            {
                _entries.Add(entry);
                if (_entries.Count > MaxEntries)
                {
                    _entries.RemoveRange(0, _entries.Count - MaxEntries);
                }
            }
        }

        public IReadOnlyList<SystemErrorEntry> GetRecent(int take)
        {
            lock (_sync)
            {
                return _entries
                    .OrderByDescending(x => x.OccurredAtUtc)
                    .Take(Math.Max(1, take))
                    .ToList();
            }
        }

        public int CountSince(DateTime sinceUtc)
        {
            lock (_sync)
            {
                return _entries.Count(x => x.OccurredAtUtc >= sinceUtc);
            }
        }
    }
}
