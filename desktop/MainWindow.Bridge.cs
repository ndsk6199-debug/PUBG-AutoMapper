using System.Windows;
using System.Windows.Interop;
using PubgAutoMapper.Services;

namespace PubgAutoMapper;

public partial class MainWindow
{
    private ControlBridge? _bridge;

    private async void Bridge_Click(object sender, RoutedEventArgs e)
    {
        if (_serial is null) return;
        BridgeButton.IsEnabled = false;
        try
        {
            _input?.Dispose();
            _bridge?.Dispose();
            _bridge = new ControlBridge(_adb);
            await _bridge.ConnectAsync(_serial);
            _input = new RealtimeInputController(_bridge)
            {
                ScreenWidth = _width,
                ScreenHeight = _height
            };
            _input.Attach(new WindowInteropHelper(this).Handle);
            StatusText.Text = "Status: Android Agent connected";
            TestTapButton.IsEnabled = true;
            InputToggleButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _input?.Dispose();
            _input = null;
            _bridge?.Dispose();
            _bridge = null;
            StatusText.Text = "Status: Agent bridge error";
            ResultsText.Text = ex.Message;
            BridgeButton.IsEnabled = true;
        }
    }

    private async void TestTap_Click(object sender, RoutedEventArgs e)
    {
        if (_bridge is null || _width <= 0 || _height <= 0) return;
        var response = await _bridge.TapAsync(_width / 2.0, _height / 2.0);
        ResultsText.Text = "Agent TAP response: " + response;
    }
}
