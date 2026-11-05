using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PrintStamp.Core;

namespace PrintStamp.AdminApp.Views;

public partial class LoginWindow : Window
{
    // verified against when the username doesn't exist, so timing doesn't reveal which names are real
    static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("dummy", 12);

    readonly IAdminStore _admins;
    readonly bool _firstRun;
    public event Action<string>? LoggedIn;

    public LoginWindow(IAdminStore admins, bool firstRun)
    {
        InitializeComponent();
        _admins = admins;
        _firstRun = firstRun;
        if (firstRun)
        {
            Heading.Text = "إنشاء حساب المسؤول";
            ConfirmPanel.IsVisible = true;
            GoButton.Content = "إنشاء";
        }
    }

    async void Go_Click(object? s, RoutedEventArgs e)
    {
        GoButton.IsEnabled = false;
        ErrorText.IsVisible = false;
        try
        {
            var user = UserBox.Text?.Trim() ?? "";
            var pass = PassBox.Text ?? "";

            if (_firstRun)
            {
                if (user.Length < 3) { Fail("اسم المستخدم قصير جدًا."); return; }
                if (pass.Length < 8) { Fail("كلمة المرور يجب ألا تقل عن 8 أحرف."); return; }
                if (pass != ConfirmBox.Text) { Fail("كلمتا المرور غير متطابقتين."); return; }

                var hash = await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(pass, 12));
                await _admins.CreateAdminAsync(user, hash);
                LoggedIn?.Invoke(user);
                return;
            }

            var stored = await _admins.GetPasswordHashAsync(user);
            bool ok = await Task.Run(() => BCrypt.Net.BCrypt.Verify(pass, stored ?? DummyHash)) && stored is not null;
            if (!ok)
            {
                await Task.Delay(700);   // slows down guessing
                Fail("اسم المستخدم أو كلمة المرور غير صحيحة.");
                return;
            }
            LoggedIn?.Invoke(user);
        }
        catch (Exception ex) { Fail("خطأ: " + ex.Message); }
        finally { GoButton.IsEnabled = true; }
    }

    void Fail(string msg) { ErrorText.Text = msg; ErrorText.IsVisible = true; }
}