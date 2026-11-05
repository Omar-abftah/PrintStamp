using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PrintStamp.PrintApp.Services;

public sealed class JobWatcher : IDisposable
{
    readonly FileSystemWatcher _w;
    readonly Channel<string> _q = Channel.CreateUnbounded<string>();
    readonly HashSet<string> _pending = new();

    public ChannelReader<string> Reader => _q.Reader;

    public JobWatcher(string dir)
    {
        Directory.CreateDirectory(dir);
        _w = new FileSystemWatcher(dir, "*.pdf") { EnableRaisingEvents = true };
        _w.Created += (_, e) => Enqueue(e.FullPath);
        _w.Renamed += (_, e) => Enqueue(e.FullPath);   // some printers write .tmp, then rename

        // files left over from a crash or a restart
        foreach (var f in Directory.GetFiles(dir, "*.pdf").OrderBy(File.GetCreationTimeUtc))
            Enqueue(f);
    }

    void Enqueue(string path)
    {
        lock (_pending) { if (!_pending.Add(path)) return; }   // Created + Renamed can both fire
        _q.Writer.TryWrite(path);
    }

    public void Release(string path) { lock (_pending) _pending.Remove(path); }

    public void Dispose() => _w.Dispose();
}

public static class FileReady
{
    // the printer may still be writing: wait until the size is stable and the file opens exclusively
    public static async Task WaitAsync(string path)
    {
        long last = -1;
        for (int i = 0; i < 50; i++)
        {
            try
            {
                long len = new FileInfo(path).Length;
                using var f = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                if (len > 0 && len == last) return;
                last = len;
            }
            catch (IOException) { }
            await Task.Delay(300);
        }
        throw new TimeoutException($"File never became readable: {path}");
    }
}