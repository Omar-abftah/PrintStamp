using PrintStamp.Core;
using PrintStamp.Data;

namespace PrintStamp.Tests;

public class StoreTests
{
    [Fact]
    public async Task Create_then_find_by_code()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ps-{Guid.NewGuid():N}.db");
        var store = new SqliteJobStore(path);
        var a = await store.AddEntityAsync("جهة أ");
        var b = await store.AddEntityAsync("جهة ب");

        var job = await store.CreateJobAsync(a, b, DateTime.Now, 3);
        var found = await store.FindByCodeAsync(job.Code.ToLowerInvariant());

        Assert.NotNull(found);
        Assert.Equal("جهة أ", found!.IssuerName);
        Assert.Equal(3, found.PageCount);
        Assert.Equal(10, job.Code.Length);

        Assert.Null(await store.GetBarcodePosAsync());
        await store.SetBarcodePosAsync(new BarcodePos(0.8, 0.9));
        Assert.Equal(new BarcodePos(0.8, 0.9), await store.GetBarcodePosAsync());
    }

    [Fact]
    public async Task Purge_expired_removes_old_jobs_and_lookup_logs()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ps-{Guid.NewGuid():N}.db");
        var store = new SqliteJobStore(path);
        var a = await store.AddEntityAsync("جهة أ");
        var b = await store.AddEntityAsync("جهة ب");

        var job = await store.CreateJobAsync(a, b, DateTime.Now, 2);
        await store.LogLookupAsync("admin", job.Code, true);

        // Cutoff 2 months ago: recent data is kept
        Assert.Equal(0, await store.PurgeExpiredAsync(Retention.CutoffUtc()));
        Assert.NotNull(await store.FindByCodeAsync(job.Code));
        Assert.Single(await store.RecentLookupsAsync(10));

        // Cutoff in the future: expired job and lookup log are purged
        Assert.Equal(1, await store.PurgeExpiredAsync(DateTime.UtcNow.AddMinutes(1)));
        Assert.Null(await store.FindByCodeAsync(job.Code));
        Assert.Empty(await store.RecentLookupsAsync(10));
    }
}