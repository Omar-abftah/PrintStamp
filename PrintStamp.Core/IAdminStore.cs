namespace PrintStamp.Core;

public record LookupLogEntry(DateTime At, string Admin, string Code, bool Found);

public class DuplicateEntityException(string name) : Exception($"Entity already exists: {name}");

public interface IAdminStore
{
    Task<bool> AnyAdminAsync();
    Task CreateAdminAsync(string username, string passwordHash);   // only works while no admin exists
    Task<string?> GetPasswordHashAsync(string username);
    Task LogLookupAsync(string admin, string code, bool found);
    Task<IReadOnlyList<LookupLogEntry>> RecentLookupsAsync(int count);
}