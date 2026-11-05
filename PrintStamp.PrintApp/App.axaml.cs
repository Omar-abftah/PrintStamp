using System;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PrintStamp.Core;
using PrintStamp.Data;
using PrintStamp.PrintApp.Services;

namespace PrintStamp.PrintApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;   // no window, but keep running

            var store = new SqliteJobStore(AppPaths.Db);
            var watcher = new JobWatcher(AppPaths.Queue);
            var cfg = AppConfig.Load();
            IPdfPrinter printer = OperatingSystem.IsWindows()
                ? new SumatraPdfPrinter(cfg.SumatraPath ?? Path.Combine(AppPaths.Root, "SumatraPDF.exe"), cfg.PrinterName)
                : new DevPdfPrinter();
            _ = new JobProcessor(store, watcher, printer).RunAsync(CancellationToken.None);
            _ = new RetentionService(store).RunAsync(CancellationToken.None);
        }
        base.OnFrameworkInitializationCompleted();
    }
}