namespace PrintStamp.Core;

public interface IJobStore
{
    // dropdowns (PrintApp) and management (AdminApp)
    Task<IReadOnlyList<Entity>> GetEntitiesAsync(bool includeInactive = false);
    Task<int> AddEntityAsync(string name);
    Task<int> PurgeExpiredAsync(DateTime cutoffUtc);
    Task RenameEntityAsync(int id, string name);
    Task SetEntityActiveAsync(int id, bool active);

    // printing
    Task<PrintJob> CreateJobAsync(int issuerId, int receiverId, DateTime createdAt, int pageCount);
    Task<BarcodePos?> GetBarcodePosAsync();
    Task SetBarcodePosAsync(BarcodePos pos);

    // scanning
    Task<JobDetails?> FindByCodeAsync(string code);
}