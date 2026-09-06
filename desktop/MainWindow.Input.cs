using System.Windows;
using System.Windows.Interop;
using PubgAutoMapper.Services;

namespace PubgAutoMapper;

public partial class MainWindow
{
    private RealtimeInputController? _input;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (_bridge is not null) _input = new RealtimeInputController(_bridge);
        }
        catch (Exception ex) { StatusText.Text = "Status: Input initialization error • " + ex.Message; }
    }

    private void InputToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_bridge is null)
        {
            StatusText.Text = "Status: Connect Android Agent first";
            return;
        }

        try
        {
            _input ??= new RealtimeInputController(_bridge)
            {
                ScreenWidth = _width,
                ScreenHeight = _height
            };
            _input.ScreenWidth = _width;
            _input.ScreenHeight = _height;
            if (!_input.IsActive)
            {
                _input.Attach(new WindowInteropHelper(this).Handle);
                _input.Start();
                InputToggleButton.Content = "Stop Keyboard + Mouse";
                StatusText.Text = "Status: Keyboard + mouse bridge ACTIVE";
            }
            else
            {
                _input.Stop();
                InputToggleButton.Content = "Start Keyboard + Mouse";
                StatusText.Text = "Status: Keyboard + mouse bridge stopped";
            }
        }
        catch (Exception ex)
        {
            _input?.Stop();
            InputToggleButton.Content = "Start Keyboard + Mouse";
            StatusText.Text = "Status: Input hook error";
            ResultsText.Text = ex.Message;
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        try { _input?.Dispose(); } catch { }
        try { _bridge?.Dispose(); } catch { }
    }
}
