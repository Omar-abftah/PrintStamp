using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PrintStamp.AdminApp.ViewModels;
using PrintStamp.AdminApp.Views;
using PrintStamp.Core;
using PrintStamp.Data;

namespace PrintStamp.AdminApp;

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
            var store = new SqliteJobStore(AppPaths.Db);
            bool firstRun = !store.AnyAdminAsync().GetAwaiter().GetResult();
            _ = store.PurgeExpiredAsync(Retention.CutoffUtc());

            var login = new LoginWindow(store, firstRun);
            login.LoggedIn += admin =>
            {
                var main = new MainWindow(store, store, admin);
                desktop.MainWindow = main;
                main.Show();
                login.Close();
            };
            desktop.MainWindow = login;
        }
        base.OnFrameworkInitializationCompleted();
    }
}