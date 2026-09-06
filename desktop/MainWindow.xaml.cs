using System.Windows;
using System.Windows.Media.Imaging;
using PubgAutoMapper.Services;

namespace PubgAutoMapper;

public partial class MainWindow : Window
{
    private readonly AdbService _adb = new();
    private string? _serial;
    private int _width;
    private int _height;

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
                ResolutionText.Text = "Resolution: —";
                return;
            }

            _serial = device.Serial;
            (_width, _height) = await _adb.GetResolutionAsync(_serial);
            StatusText.Text = $"Status: Connected • {device.Model} • {_serial}";
            ResolutionText.Text = $"Resolution: {_width} × {_height}";
            CoordinateText.Text = "Coordinate inspector: click the screen after capture";

            var png = await _adb.CapturePngAsync(_serial);
            using var ms = new MemoryStream(png);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            ScreenPreview.Source = image;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Status: ADB error";
            ResolutionText.Text = "Resolution: —";
            CoordinateText.Text = ex.Message;
        }
        finally { RefreshButton.IsEnabled = true; }
    }

    private void Screen_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_width <= 0 || _height <= 0) return;
        var p = e.GetPosition(ScreenPreview);
        var x = Math.Clamp(p.X / ScreenPreview.ActualWidth * _width, 0, _width - 1);
        var y = Math.Clamp(p.Y / ScreenPreview.ActualHeight * _height, 0, _height - 1);
        var nx = x / _width;
        var ny = y / _height;
        CoordinateText.Text = KeepNormalized.IsChecked == true
            ? $"Coordinate: px ({x:0}, {y:0}) • normalized ({nx:0.000000}, {ny:0.000000})"
            : $"Coordinate: px ({x:0}, {y:0})";
    }
}
