using System.Windows;
using System.Windows.Controls;
using System.Management;
using SmartBrightness.Core;

namespace SmartBrightness.App;

public partial class MainWindow : Window
{
    private readonly BrightnessEngine _engine = new();
    private readonly WindowsBrightnessController _brightnessController = new();
    private readonly LocalSettingsStore _settingsStore = new();

    public MainWindow() => InitializeComponent();

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = await _settingsStore.LoadSettingsAsync();
            BrightnessSlider.Minimum = settings.Minimum;
            BrightnessSlider.Maximum = settings.Maximum;
            BrightnessSlider.Value = settings.Brightness;
            var displays = await _brightnessController.DetectAsync();
            StatusText.Text = displays.Count == 0
                ? "No WMI brightness control detected. Automatic adjustment is off."
                : $"{displays.Count} internal display(s) support Windows brightness control.";
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            StatusText.Text = "Display diagnostics unavailable. Automatic adjustment is off.";
        }
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BrightnessValue is not null) BrightnessValue.Text = $"{e.NewValue:0}%";
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        var decision = _engine.Update(new SensorSnapshot(null, null, null, DateTimeOffset.Now), BrightnessSlider.Value);
        BrightnessValue.Text = $"{decision.Target:0}%";
        try
        {
            var count = await _brightnessController.SetAllAsync((byte)Math.Round(decision.Target));
            await _settingsStore.SaveSettingsAsync(new UserSettings(decision.Target, BrightnessSlider.Minimum, BrightnessSlider.Maximum));
            await _settingsStore.AppendActivityAsync(decision.Target);
            StatusText.Text = count == 0 ? "No supported internal display was changed." : $"Brightness applied to {count} internal display(s).";
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            StatusText.Text = "Windows could not change brightness on a supported internal display.";
        }
    }

    private void AutoMode_Click(object sender, RoutedEventArgs e)
    {
        AutoMode.IsChecked = false;
        StatusText.Text = "Automatic mode requires supported sensors and display control.";
        MessageBox.Show(this, "Automatic control is unavailable until compatible sensors and sampling are configured. Manual WMI control may be available for supported internal displays.", "Automatic control unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
