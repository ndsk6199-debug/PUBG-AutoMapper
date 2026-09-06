using System.Windows;
namespace PubgAutoMapper;
public partial class MainWindow : Window
{
    public MainWindow() { InitializeComponent(); }
    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Status: Device discovery module not connected yet";
        ResolutionText.Text = "Resolution: awaiting ADB";
        CoordinateText.Text = "Coordinate inspector: ready for capture module";
    }
}
