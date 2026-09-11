using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
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
        GenerateButton.IsEnabled = false;
        BridgeButton.IsEnabled = false;
        TestTapButton.IsEnabled = false;
        InputToggleButton.IsEnabled = false;
        Overlay.Children.Clear();
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
            BridgeButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Status: ADB error";
            ResolutionText.Text = "Resolution: —";
            CoordinateText.Text = ex.Message;
            DetectHudButton.IsEnabled = false;
            BridgeButton.IsEnabled = false;
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
                ? "No candidate controls detected. Use a clean gameplay HUD screenshot and try again."
                : string.Join(Environment.NewLine, _lastPoints.Select(p => $"{p.Name}: ({p.X:0.000000}, {p.Y:0.000000})  confidence={p.Confidence:0.00}"));
            StatusText.Text = $"Status: HUD analysis complete • {_lastPoints.Count} candidate(s)";
            GenerateButton.IsEnabled = _lastPoints.Count > 0;
            BridgeButton.IsEnabled = _serial is not null;
            DrawOverlay();
        }
        catch (Exception ex) { ResultsText.Text = "HUD error: " + ex.Message; }
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (_lastPoints.Count == 0) return;
        var dlg = new SaveFileDialog
        {
            Filter = "JSON profile (*.json)|*.json",
            FileName = "PUBG-AutoMapper-Detected.json",
            AddExtension = true
        };
        if (dlg.ShowDialog() != true) return;

        var profile = new HudProfile("PUBG Mobile", _width, _height, _lastPoints);
        File.WriteAllText(dlg.FileName, HudProfileGenerator.ToJson(profile));
        ResultsText.Text += Environment.NewLine + Environment.NewLine + "Saved: " + dlg.FileName;
    }

    private void DrawOverlay()
    {
        Overlay.Children.Clear();
        if (_lastPoints.Count == 0 || Overlay.ActualWidth <= 0 || Overlay.ActualHeight <= 0) return;

        double controlW = Overlay.ActualWidth;
        double controlH = Overlay.ActualHeight;
        double imageAspect = _width / (double)_height;
        double controlAspect = controlW / controlH;
        double drawW, drawH, ox, oy;
        if (imageAspect > controlAspect)
        {
            drawW = controlW; drawH = drawW / imageAspect; ox = 0; oy = (controlH - drawH) / 2;
        }
        else
        {
            drawH = controlH; drawW = drawH * imageAspect; oy = 0; ox = (controlW - drawW) / 2;
        }

        foreach (var point in _lastPoints)
        {
            double x = ox + point.X * drawW;
            double y = oy + point.Y * drawH;
            var dot = new Ellipse { Width = 14, Height = 14, Stroke = Brushes.White, StrokeThickness = 2, Fill = Brushes.Red, ToolTip = $"{point.Name} • {point.X:0.000000}, {point.Y:0.000000} • {point.Confidence:P0}" };
            Canvas.SetLeft(dot, x - 7); Canvas.SetTop(dot, y - 7); Overlay.Children.Add(dot);
            var label = new Border { Background = new SolidColorBrush(Color.FromArgb(205, 0, 0, 0)), Padding = new Thickness(4, 2, 4, 2), Child = new TextBlock { Text = point.Name, Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.SemiBold } };
            Canvas.SetLeft(label, x + 9); Canvas.SetTop(label, y - 9); Overlay.Children.Add(label);
        }
    }

    private void ScreenPreview_SizeChanged(object sender, SizeChangedEventArgs e) => DrawOverlay();

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
