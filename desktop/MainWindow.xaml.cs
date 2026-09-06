using System.Windows;
using System.Windows.Media.Imaging;
using PubgAutoMapper.Services;

namespace PubgAutoMapper;

public partial class MainWindow : Window
{
    private readonly AdbService _adb = new();
    private readonly HudDetector _hud = new();
    private string? _serial;
    private int _width;
    private int _height;
    private byte[]? _lastPng;
    private IReadOnlyList<HudPoint> _lastPoints = Array.Empty<HudPoint>();

    public MainWindow() { InitializeComponent(); }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;
        try
        {
            var devices = await _adb.GetDevicesAsync();
            var device = devices.FirstOrDefault(d => d.State == "device");
            if (device is null)
            {
                StatusText.Text = devices.Count == 0 ? "Status: No ADB device detected" : "Status: Device detected but not authorized/ready";
                return;
            }
            _serial = device.Serial;
            (_width, _height) = await _adb.GetResolutionAsync(_serial);
            _lastPng = await _adb.CapturePngAsync(_serial);
            using var ms = new MemoryStream(_lastPng);
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = ms; image.EndInit();
            ScreenPreview.Source = image;
            StatusText.Text = $"Status: Connected • {device.Model} • {_serial}";
            ResolutionText.Text = $"Resolution: {_width} × {_height}";
            CoordinateText.Text = "Coordinate inspector: click the screen";
            ResultsText.Text = "Screen captured. Press Analyze HUD.";
            DetectHudButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Status: ADB error";
            ResolutionText.Text = "Resolution: —";
            CoordinateText.Text = ex.Message;
            DetectHudButton.IsEnabled = false;
        }
        finally { RefreshButton.IsEnabled = true; }
    }

    private void DetectHud_Click(object sender, RoutedEventArgs e)
    {
        if (_lastPng is null || _width <= 0 || _height <= 0) return;
        try
        {
            _lastPoints = _hud.Detect(_lastPng, _width, _height);
            ResultsText.Text = _lastPoints.Count == 0
                ? "No confident HUD controls detected. Try a clean in-game HUD screenshot."
                : string.Join(Environment.NewLine, _lastPoints.Select(p => $"{p.Name}: ({p.X:0.000000}, {p.Y:0.000000})  confidence={p.Confidence:0.00}"));
            StatusText.Text = $"Status: HUD analysis complete • {_lastPoints.Count} candidate(s)";
        }
        catch (Exception ex) { ResultsText.Text = "HUD error: " + ex.Message; }
    }

    private void Screen_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_width <= 0 || _height <= 0 || ScreenPreview.ActualWidth <= 0 || ScreenPreview.ActualHeight <= 0) return;
        var p = e.GetPosition(ScreenPreview);
        var x = Math.Clamp(p.X / ScreenPreview.ActualWidth * _width, 0, _width - 1);
        var y = Math.Clamp(p.Y / ScreenPreview.ActualHeight * _height, 0, _height - 1);
        var nx = x / _width; var ny = y / _height;
        CoordinateText.Text = KeepNormalized.IsChecked == true ? $"Coordinate: px ({x:0}, {y:0}) • normalized ({nx:0.000000}, {ny:0.000000})" : $"Coordinate: px ({x:0}, {y:0})";
    }
}
