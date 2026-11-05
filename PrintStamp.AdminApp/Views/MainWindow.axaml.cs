using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PrintStamp.Core;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using PrintStamp.AdminApp.Services;

namespace PrintStamp.AdminApp.Views;

public partial class MainWindow : Window
{
    sealed record EntityRow(Entity E)
    {
        public override string ToString() => E.IsActive ? E.Name : $"{E.Name}   (معطّل)";
    }

    readonly IJobStore _jobs;
    readonly IAdminStore _admins;
    readonly string _admin;

    public MainWindow(IJobStore jobs, IAdminStore admins, string admin)
    {
        InitializeComponent();
        _jobs = jobs; _admins = admins; _admin = admin;
        Title += " — " + admin;

        CodeBox.KeyDown += CodeBox_KeyDown;
        Opened += async (_, _) => { await ReloadEntities(); await ReloadLog(); CodeBox.Focus(); };
        Activated += (_, _) => { if (Tabs.SelectedIndex == 0) CodeBox.Focus(); };   // scanner always types into the box
        Tabs.SelectionChanged += (_, e) => { if (e.Source == Tabs && Tabs.SelectedIndex == 0) CodeBox.Focus(); };
        EntityNameBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) UpdateEntityButtons(); };
    }

    // ---------------- scan ----------------
    async void CodeBox_KeyDown(object? s, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;

        var code = CodeGenerator.Normalize(CodeBox.Text ?? "");
        CodeBox.Text = "";                 // empty right away, ready for the next scan
        if (code.Length == 0) return;

        try
        {
            // the scanner types through the keyboard layout: with Arabic active, letters come out Arabic
            if (code.Any(ch => ch > 127))
            {
                ShowError("الرمز يحتوي على أحرف غير إنجليزية. غيّر لغة الكيبورد إلى English وامسح مرة أخرى.");
                return;
            }

            var d = await _jobs.FindByCodeAsync(code);
            await _admins.LogLookupAsync(_admin, code, d is not null);

            if (d is null) ShowError($"لا يوجد سجل بهذا الرمز: {code}\n(قد تكون مدة الحفظ قد انتهت)");
            else ShowResult(d);
            await ReloadLog();
        }
        catch (Exception ex) { ShowError("خطأ: " + ex.Message); }
        finally { CodeBox.Focus(); }
    }

    void ShowResult(JobDetails d)
    {
        ResError.IsVisible = false;
        ResCode.Text     = d.Code;
        ResIssuer.Text   = $"الجهة الصادرة: {d.IssuerName}";
        ResReceiver.Text = $"الجهة المستلمة: {d.ReceiverName}";
        ResDate.Text     = $"التاريخ والوقت: {d.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}";
        ResPages.Text    = $"عدد الصفحات: {d.PageCount}";
    }

    void ShowError(string msg)
    {
        ResCode.Text = ResIssuer.Text = ResReceiver.Text = ResDate.Text = ResPages.Text = "";
        ResError.Text = msg;
        ResError.IsVisible = true;
    }

    async Task ReloadLog()
    {
        var rows = await _admins.RecentLookupsAsync(20);
        LogList.ItemsSource = rows.Select(l =>
            $"{l.At.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}   {l.Admin}   {l.Code}   {(l.Found ? "✔" : "✘")}").ToList();
    }

    // ---------------- entities ----------------
    async Task ReloadEntities()
    {
        var all = await _jobs.GetEntitiesAsync(includeInactive: true);
        EntityList.ItemsSource = all.Select(x => new EntityRow(x)).ToList();
        UpdateEntityButtons();
    }

    void UpdateEntityButtons()
    {
        var sel = EntityList.SelectedItem as EntityRow;
        var name = EntityNameBox.Text?.Trim() ?? "";

        AddBtn.IsEnabled    = sel is null && name.Length > 0;                          // adding only when nothing is selected
        RenameBtn.IsEnabled = sel is not null && name.Length > 0 && name != sel.E.Name; // saving only when the name changed
        ToggleBtn.IsEnabled = sel is not null;
        NewBtn.IsEnabled    = sel is not null;
        EntityModeText.Text = sel is null ? "جهة جديدة" : $"تعديل: {sel.E.Name}";
    }

    void EntityList_SelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        if (EntityList.SelectedItem is EntityRow r) EntityNameBox.Text = r.E.Name;
        UpdateEntityButtons();
    }

    void NewEntity_Click(object? s, RoutedEventArgs e)
    {
        EntityList.SelectedItem = null;
        EntityNameBox.Text = "";
        UpdateEntityButtons();
    }

    async void AddEntity_Click(object? s, RoutedEventArgs e) => await RunEntityAction(async name =>
    {
        await _jobs.AddEntityAsync(name);
        EntityNameBox.Text = "";
    });

    async void Rename_Click(object? s, RoutedEventArgs e) => await RunEntityAction(async name =>
    {
        if (EntityList.SelectedItem is EntityRow r) await _jobs.RenameEntityAsync(r.E.Id, name);
        EntityNameBox.Text = "";
    });

    async void Toggle_Click(object? s, RoutedEventArgs e)
    {
        if (EntityList.SelectedItem is not EntityRow r) return;
        EntityMsg.Text = "";
        try { await _jobs.SetEntityActiveAsync(r.E.Id, !r.E.IsActive); await ReloadEntities(); }
        catch (Exception ex) { EntityMsg.Text = ex.Message; }
    }

    async Task RunEntityAction(Func<string, Task> action)
    {
        EntityMsg.Text = "";
        var name = EntityNameBox.Text?.Trim() ?? "";
        if (name.Length == 0) { EntityMsg.Text = "اكتب اسم الجهة أولًا."; return; }
        try { await action(name); await ReloadEntities(); }
        catch (DuplicateEntityException) { EntityMsg.Text = "هذا الاسم موجود بالفعل."; }
        catch (Exception ex) { EntityMsg.Text = ex.Message; }
    }
    
    async void Wipe_Click(object? s, RoutedEventArgs e)
    {
        var dlg = new WipeDialog(_admins, _admin, () =>
        {
            Uninstaller.Start();   // throws if the folder looks wrong, and the dialog shows the message
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        });
        await dlg.ShowDialog(this);
    }
}