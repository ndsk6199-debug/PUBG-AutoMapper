using System.Windows;
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
            _bridge?.Dispose();
            _bridge = new ControlBridge(_adb);
            await _bridge.ConnectAsync(_serial);
            StatusText.Text = "Status: Android Agent connected";
            TestTapButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
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
