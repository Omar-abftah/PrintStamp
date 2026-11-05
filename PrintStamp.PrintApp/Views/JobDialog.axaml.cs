using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using PrintStamp.Core;
using System.Globalization;

namespace PrintStamp.PrintApp.Views;

public record JobInput(int IssuerId, int ReceiverId, DateTime CreatedAt, BarcodePos BarcodePos);

public partial class JobDialog : Window
{
    readonly TaskCompletionSource<JobInput?> _tcs = new();
    double _aspect = 0.707;             // page width / height, set from the rendered preview
    BarcodePos _pos = new(0, 0);        // top-left of the barcode, fractions of the page
    bool _posChosen;                    // false until the user clicks (first run only)
    bool _dragging;
    private DateTime _createdAt;

    public JobDialog()
    {
        InitializeComponent();
        Issuer.SelectionChanged   += (_, _) => Refresh();
        Receiver.SelectionChanged += (_, _) => Refresh();
        Overlay.SizeChanged    += (_, _) => UpdateMarker();
        Overlay.PointerPressed += Overlay_PointerPressed;
        Overlay.PointerMoved   += Overlay_PointerMoved;
        Overlay.PointerReleased += Overlay_PointerReleased;
    }

    public static Task<JobInput?> AskAsync(IReadOnlyList<Entity> entities, DateTime now,
                                           Bitmap preview, BarcodePos? saved)
    {
        var d = new JobDialog();
        d._aspect = (double)preview.PixelSize.Width / preview.PixelSize.Height;
        d.PreviewImage.Source = preview;
        d._posChosen = saved is not null;
        d._pos = BarcodeLayout.Clamp(saved ?? new BarcodePos(1, 1), d._aspect);   // default: bottom-right

        d.Issuer.ItemsSource = entities;
        d.Receiver.ItemsSource = entities;
        d._createdAt = now;
        d.StampText.Text = now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        d.Show();
        d.Activate();
        d.Refresh();
        return d._tcs.Task;
    }

    void Refresh() => Ok.IsEnabled =
        Issuer.SelectedItem is not null && Receiver.SelectedItem is not null
        && _posChosen;

    // ---------- marker ----------
    void UpdateMarker()
    {
        double w = Overlay.Bounds.Width, h = Overlay.Bounds.Height;
        Marker.IsVisible = _posChosen && w > 0 && h > 0;
        if (!Marker.IsVisible) return;

        Marker.Width  = BarcodeLayout.WidthFraction * w;
        Marker.Height = BarcodeLayout.HeightFraction(_aspect) * h;
        Canvas.SetLeft(Marker, _pos.X * w);
        Canvas.SetTop(Marker, _pos.Y * h);
    }

    void Overlay_PointerPressed(object? s, PointerPressedEventArgs e)
    {
        _dragging = true;
        e.Pointer.Capture(Overlay);
        MoveTo(e);
    }

    void Overlay_PointerMoved(object? s, PointerEventArgs e) { if (_dragging) MoveTo(e); }

    void Overlay_PointerReleased(object? s, PointerReleasedEventArgs e)
    {
        _dragging = false;
        e.Pointer.Capture(null);
    }

    void MoveTo(PointerEventArgs e)
    {
        double w = Overlay.Bounds.Width, h = Overlay.Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var p = e.GetPosition(Overlay);
        // center the barcode on the pointer, then keep it inside the page
        var raw = new BarcodePos(p.X / w - BarcodeLayout.WidthFraction / 2,
                                 p.Y / h - BarcodeLayout.HeightFraction(_aspect) / 2);
        _pos = BarcodeLayout.Clamp(raw, _aspect);
        _posChosen = true;
        UpdateMarker();
        Refresh();
    }

    // ---------- buttons ----------
    void Ok_Click(object? s, RoutedEventArgs e)
    {
        var issuer = (Entity)Issuer.SelectedItem!;
        var receiver = (Entity)Receiver.SelectedItem!;
        _tcs.TrySetResult(new JobInput(issuer.Id, receiver.Id, _createdAt, _pos));
        Close();
    }

    void Cancel_Click(object? s, RoutedEventArgs e) => Close();

    // closing with X counts as cancel: nothing gets printed
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        (PreviewImage.Source as IDisposable)?.Dispose();
        _tcs.TrySetResult(null);
    }
}