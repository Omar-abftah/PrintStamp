using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PrintStamp.Core;

namespace PrintStamp.AdminApp.Views;

public partial class WipeDialog : Window
{
    readonly IAdminStore _admins;
    readonly string _admin;
    readonly Action _wipe;

    public WipeDialog(IAdminStore admins, string admin, Action wipe)
    {
        InitializeComponent();
        _admins = admins; _admin = admin; _wipe = wipe;
        ConfirmBox.PropertyChanged += (_, e) =>
            { if (e.Property == TextBox.TextProperty) GoBtn.IsEnabled = ConfirmBox.Text?.Trim() == "حذف"; };
    }

    async void Go_Click(object? s, RoutedEventArgs e)
    {
        GoBtn.IsEnabled = false;
        Err.IsVisible = false;
        try
        {
            var stored = await _admins.GetPasswordHashAsync(_admin);
            var pass = PassBox.Text ?? "";
            bool ok = stored is not null && await Task.Run(() => BCrypt.Net.BCrypt.Verify(pass, stored));
            if (!ok)
            {
                await Task.Delay(1000);
                Fail("كلمة المرور غير صحيحة.");
                return;
            }
            _wipe();   // starts the cleanup script and shuts the app down
        }
        catch (Exception ex) { Fail("تعذّر الحذف: " + ex.Message); }
        finally { GoBtn.IsEnabled = ConfirmBox.Text?.Trim() == "حذف"; }
    }

    void Fail(string msg) { Err.Text = msg; Err.IsVisible = true; }
    void Cancel_Click(object? s, RoutedEventArgs e) => Close();
}