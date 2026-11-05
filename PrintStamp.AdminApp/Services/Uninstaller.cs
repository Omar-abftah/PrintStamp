using System;
using System.Diagnostics;
using System.IO;
using PrintStamp.Core;

namespace PrintStamp.AdminApp.Services;

public static class Uninstaller
{
    public static void Start()
    {
        var root = Path.GetFullPath(AppPaths.Root).TrimEnd(Path.DirectorySeparatorChar);
        if (!SafeToDelete(root)) throw new InvalidOperationException($"Refusing to delete this folder: {root}");

        if (OperatingSystem.IsWindows()) StartWindows(root); else StartUnix(root);
    }

    // a wrong PRINTSTAMP_HOME must never turn this into "delete my home folder"
    static bool SafeToDelete(string root)
    {
        var sep = Path.DirectorySeparatorChar;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(sep);
        var drive = (Path.GetPathRoot(root) ?? "").TrimEnd(sep);
        return root.Length > 3
            && !string.Equals(root, home, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(root, drive, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(Path.Combine(root, "data"));   // looks like our folder
    }

    static void StartWindows(string root)
{
    var script = Path.Combine(Path.GetTempPath(), $"printstamp-uninstall-{Guid.NewGuid():N}.ps1");
    var r = root.Replace("'", "''");
    var pid = Environment.ProcessId;

    File.WriteAllText(script, $$"""
        $log = Join-Path $env:TEMP 'printstamp-uninstall.log'
        Start-Transcript -Path $log -Force | Out-Null
        $root = '{{r}}'

        Wait-Process -Id {{pid}} -Timeout 15 -ErrorAction SilentlyContinue
        Get-Process -Name 'PrintStamp.AdminApp','PrintStamp.PrintApp' -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Seconds 1

        schtasks /Delete /TN 'PrintStamp' /F 2>$null | Out-Null
        Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'PrintStamp' -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath 'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup\PrintStamp.lnk' -Force -ErrorAction SilentlyContinue

        for ($i = 0; $i -lt 15 -and (Test-Path -LiteralPath $root); $i++) {
            if ($i -eq 2) {   # still there: take ownership and reset the locked-down permissions
                takeown /F $root /R /D Y | Out-Null
                icacls $root /grant 'Administrators:(OI)(CI)F' /T /C | Out-Null
            }
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction Continue
            Start-Sleep -Seconds 1
        }

        if (Test-Path -LiteralPath $root) {
            'FAILED: folder still exists. Remaining files:'
            Get-ChildItem -LiteralPath $root -Recurse -Force | Select-Object -First 20 -ExpandProperty FullName
        } else {
            'OK: deleted'
        }
        Stop-Transcript | Out-Null
        if (-not (Test-Path -LiteralPath $root)) { Remove-Item -LiteralPath $PSCommandPath -Force }
        """);

    Process.Start(new ProcessStartInfo("powershell.exe")
    {
        Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"",
        UseShellExecute = true,
        Verb = "runas",                          // UAC prompt: needed for other users' processes and the locked bin folder
        WindowStyle = ProcessWindowStyle.Hidden,
    });
}

    // Fedora / development, so you can test the whole thing without touching Windows
    static void StartUnix(string root)
    {
        var q = root.Replace("'", "'\\''");
        // [P] trick: the pattern doesn't match this shell's own command line
        Process.Start(new ProcessStartInfo("/bin/sh")
        {
            ArgumentList = { "-c", $"sleep 2; pkill -f '[P]rintStamp.PrintApp'; rm -rf -- '{q}'" },
            UseShellExecute = false,
        });
    }
}