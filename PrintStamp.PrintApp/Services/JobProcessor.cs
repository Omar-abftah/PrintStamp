using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using iText.Kernel.Pdf;
using PrintStamp.Core;
using PrintStamp.PrintApp.Views;

namespace PrintStamp.PrintApp.Services;

public sealed class JobProcessor(IJobStore store, JobWatcher watcher, IPdfPrinter printer)
{
    // one job at a time, so five quick prints never open five dialogs at once
    public async Task RunAsync(CancellationToken ct)
    {
        await foreach (var path in watcher.Reader.ReadAllAsync(ct))
        {
            try { await HandleAsync(path); }
            catch (Exception ex) { Console.Error.WriteLine($"Job failed for {path}: {ex}"); }
            finally { watcher.Release(path); }
        }
    }

    async Task HandleAsync(string path)
    {
        await FileReady.WaitAsync(path);

        int pages;
        using (var pdf = new PdfDocument(new PdfReader(path))) pages = pdf.GetNumberOfPages();

        var entities = await store.GetEntitiesAsync();
        var saved = await store.GetBarcodePosAsync();
        var preview = await Task.Run(() => PdfPreview.RenderFirstPage(path));   // off the UI thread

        var input = await Dispatcher.UIThread.InvokeAsync(
            () => JobDialog.AskAsync(entities, DateTime.Now, preview, saved));

        if (input is null) { File.Delete(path); return; }   // cancelled: print nothing

        await store.SetBarcodePosAsync(input.BarcodePos);   // last confirmed position = new default
        var job = await store.CreateJobAsync(input.IssuerId, input.ReceiverId, input.CreatedAt, pages);

        HiddenFolder.Ensure(AppPaths.Work);
        var stamped = Path.Combine(AppPaths.Work, job.Code + ".pdf");
        PdfStamper.Stamp(path, stamped, job.Code, input.BarcodePos);

        try
        {
            await printer.PrintAsync(stamped);
        }
        catch
        {
            // the DB row already exists, so keep the stamped file for a retry instead of making a second code
            HiddenFolder.Ensure(AppPaths.Failed);
            File.Move(stamped, Path.Combine(AppPaths.Failed, job.Code + ".pdf"), overwrite: true);
            File.Delete(path);
            throw;
        }

        HiddenFolder.Ensure(AppPaths.Done);
        File.Move(stamped, Path.Combine(AppPaths.Done, job.Code + ".pdf"), overwrite: true);
        File.Delete(path);   // the unstamped original is never kept
        Console.WriteLine($"Job {job.Code}: {pages} page(s) stamped and sent to print");
    }
}