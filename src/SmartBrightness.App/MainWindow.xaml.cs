using System.Windows;
using System.Windows.Controls;
using SmartBrightness.Core;

namespace SmartBrightness.App;

public partial class MainWindow : Window
{
    private readonly BrightnessEngine _engine = new();

    public MainWindow() => InitializeComponent();

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BrightnessValue is not null) BrightnessValue.Text = $"{e.NewValue:0}%";
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var decision = _engine.Update(new SensorSnapshot(null, null, null, DateTimeOffset.Now), BrightnessSlider.Value);
        BrightnessValue.Text = $"{decision.Target:0}%";
        StatusText.Text = "Manual level selected. Display control is not yet connected.";
    }

    private void AutoMode_Click(object sender, RoutedEventArgs e)
    {
        AutoMode.IsChecked = false;
        StatusText.Text = "Automatic mode requires supported sensors and display control.";
        MessageBox.Show(this, "Automatic control is unavailable until compatible sensors and a safe display-control method are connected.", "Hardware not configured", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
