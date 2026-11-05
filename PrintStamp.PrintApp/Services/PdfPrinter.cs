using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using PrintStamp.Core;

namespace PrintStamp.PrintApp.Services;

public interface IPdfPrinter { Task PrintAsync(string pdfPath); }

// Fedora / development: no printer, just drop the stamped file in a folder
public sealed class DevPdfPrinter : IPdfPrinter
{
    public Task PrintAsync(string pdfPath)
    {
        Directory.CreateDirectory(AppPaths.Printed);
        File.Copy(pdfPath, Path.Combine(AppPaths.Printed, Path.GetFileName(pdfPath)), overwrite: true);
        return Task.CompletedTask;
    }
}

// Windows: SumatraPDF's command line (silent, no window)
public sealed class SumatraPdfPrinter : IPdfPrinter
{
    readonly string _exe, _printer;

    public SumatraPdfPrinter(string exePath, string? printerName)
    {
        if (!File.Exists(exePath))
            throw new FileNotFoundException("SumatraPDF not found. Check SumatraPath in config.json.", exePath);
        // no default-printer fallback on purpose (see above)
        if (string.IsNullOrWhiteSpace(printerName))
            throw new InvalidOperationException("PrinterName is missing in config.json.");
        _exe = exePath; _printer = printerName;
    }

    public async Task PrintAsync(string pdfPath)
    {
        var psi = new ProcessStartInfo(_exe) { UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("-print-to");
        psi.ArgumentList.Add(_printer);
        psi.ArgumentList.Add("-silent");
        psi.ArgumentList.Add(pdfPath);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Could not start SumatraPDF.");
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0) throw new InvalidOperationException($"SumatraPDF exited with code {proc.ExitCode}.");
    }
}