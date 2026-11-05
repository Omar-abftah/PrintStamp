using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PrintStamp.Core;

namespace PrintStamp.PrintApp.Services;

public sealed class RetentionService(IJobStore store)
{
    // at startup, then every 6 hours: covers a PC that was off for a long time
    public async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            try
            {
                var cutoff = Retention.CutoffUtc();
                int jobs = await store.PurgeExpiredAsync(cutoff);
                int files = PurgeFiles(cutoff, AppPaths.Done, AppPaths.Failed, AppPaths.Printed);
                if (jobs + files > 0) Console.WriteLine($"Retention: removed {jobs} job(s), {files} file(s)");
            }
            catch (Exception ex) { Console.Error.WriteLine($"Retention failed: {ex.Message}"); }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    static int PurgeFiles(DateTime cutoffUtc, params string[] dirs)
    {
        int n = 0;
        foreach (var dir in dirs.Where(Directory.Exists))
        foreach (var f in Directory.EnumerateFiles(dir))
        {
            if (File.GetLastWriteTimeUtc(f) >= cutoffUtc) continue;
            try { File.Delete(f); n++; } catch (IOException) { }
        }
        return n;
    }
}