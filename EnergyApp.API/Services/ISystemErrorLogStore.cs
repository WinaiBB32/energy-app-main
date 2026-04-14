namespace EnergyApp.API.Services
{
    public interface ISystemErrorLogStore
    {
        void Add(SystemErrorEntry entry);
        IReadOnlyList<SystemErrorEntry> GetRecent(int take);
        int CountSince(DateTime sinceUtc);
    }
}
