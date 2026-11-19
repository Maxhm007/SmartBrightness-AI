using System.ComponentModel;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using SmartBrightness.Core;
using Forms = System.Windows.Forms;

namespace SmartBrightness.App;

public partial class MainWindow : Window
{
    private static readonly TimeSpan SamplingInterval = TimeSpan.FromMinutes(1);
    private readonly WindowsBrightnessController _brightnessController = new();
    private readonly LocalSettingsStore _settingsStore = new();
    private readonly WebcamSampler _webcamSampler = new();
    private readonly ScreenContentSampler _screenSampler = new();
    private readonly CancellationTokenSource _updateCancellation = new();
    private readonly BackgroundUpdateService _updateService;
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ToolStripMenuItem? _trayAutoItem;
    private CancellationTokenSource? _automaticCancellation;
    private Task? _automaticLoop;
    private int? _cameraIndex;
    private int _preferredCameraIndex;
    private int _supportedDisplayCount;
    private bool _isLoaded;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        _updateService = new BackgroundUpdateService(StartUpdateInstaller);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _ = _updateService.RunAsync(_updateCancellation.Token);
        _ = RegisterApplicationRestart(null, 0);
        try
        {
            var settings = await _settingsStore.LoadSettingsAsync();
            BrightnessSlider.Minimum = settings.Minimum;
            BrightnessSlider.Maximum = settings.Maximum;
            BrightnessSlider.Value = settings.Brightness;
            OpacitySlider.Value = settings.WindowOpacity * 100;
            _preferredCameraIndex = settings.CameraIndex;
            InitializeTrayIcon();
            var displays = await _brightnessController.DetectAsync();
            _supportedDisplayCount = displays.Count;
            StatusText.Text = displays.Count == 0
                ? "No supported internal display found. Brightness control is unavailable."
                : $"Ready · {displays.Count} internal display(s) support brightness control.";
            _isLoaded = true;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or IOException)
        {
            StatusText.Text = "Display diagnostics unavailable. Brightness control may not be supported.";
            InitializeTrayIcon();
            _isLoaded = true;
        }
    }

    private void InitializeTrayIcon()
    {
        if (_trayIcon is not null) return;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open SmartBrightness AI", null, (_, _) => RestoreWindow());
        _trayAutoItem = new Forms.ToolStripMenuItem("Automatic brightness")
        {
            CheckOnClick = false,
            Checked = _automaticCancellation is not null
        };
        _trayAutoItem.Click += async (_, _) =>
        {
            AutoMode.IsChecked = _automaticCancellation is null;
            await AutoMode_ClickAsync();
        };
        menu.Items.Add(_trayAutoItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) => await ExitAsync());
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "SmartBrightness AI",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => RestoreWindow();
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BrightnessValue is not null) BrightnessValue.Text = $"{e.NewValue:0}%";
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        await StopAutomaticAsync();
        AutoMode.IsChecked = false;
        var decision = new BrightnessEngine().Update(new SensorSnapshot(null, null, null, DateTimeOffset.Now), BrightnessSlider.Value);
        try { await ApplyBrightnessAsync(decision.Target, null, null); }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or IOException)
        {
            StatusText.Text = "Windows could not change brightness on the selected display.";
        }
    }

    private async void AutoMode_Click(object sender, RoutedEventArgs e) => await AutoMode_ClickAsync();

    private async Task AutoMode_ClickAsync()
    {
        if (!_isLoaded) return;
        if (AutoMode.IsChecked == true)
            await StartAutomaticAsync();
        else
            await StopAutomaticAsync();
    }

    private async Task StartAutomaticAsync()
    {
        if (_automaticCancellation is not null) return;
        var automaticCancellation = new CancellationTokenSource();
        _automaticCancellation = automaticCancellation;
        var cancellationToken = automaticCancellation.Token;
        AutoMode.IsEnabled = false;
        StatusText.Text = "Checking display and camera support…";
        try
        {
            _supportedDisplayCount = (await _brightnessController.DetectAsync(cancellationToken)).Count;
            if (_supportedDisplayCount == 0)
            {
                await StopAutomaticAsync();
                AutoMode.IsChecked = false;
                StatusText.Text = "Automatic mode needs a supported internal display.";
                return;
            }

            if (_cameraIndex is null)
                _cameraIndex = await _webcamSampler.FindFirstAvailableAsync(_preferredCameraIndex, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_cameraIndex is { } selectedCamera) _preferredCameraIndex = selectedCamera;

            CameraStatus.Text = _cameraIndex is null
                ? "No camera found; using screen and time fallback."
                : $"Camera {_cameraIndex.Value + 1} · sampled briefly each minute.";
            ScreenStatus.Text = "Screen luminance sampled across connected displays each minute.";
            var settings = await _settingsStore.LoadSettingsAsync();
            await _settingsStore.SaveSettingsAsync(settings with { CameraIndex = _cameraIndex ?? 0, WindowOpacity = Opacity }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_trayAutoItem is not null)
            {
                _trayAutoItem.Checked = true;
                _trayAutoItem.Text = "Pause automatic brightness";
            }
            AutoMode.Content = "Automatic on";
            StatusText.Text = "Automatic brightness is on · sampling every minute.";
            _automaticLoop = RunAutomaticLoopAsync(_automaticCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            await StopAutomaticAsync();
            AutoMode.IsChecked = false;
            StatusText.Text = "Automatic mode stopped.";
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or IOException)
        {
            await StopAutomaticAsync();
            AutoMode.IsChecked = false;
            StatusText.Text = "Automatic mode could not access a supported display.";
        }
        finally
        {
            AutoMode.IsEnabled = true;
            if (_automaticCancellation is null) AutoMode.Content = "Automatic off";
        }
    }

    private async Task RunAutomaticLoopAsync(CancellationToken cancellationToken)
    {
        var engine = new BrightnessEngine();
        var firstSample = true;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var cameraTask = _cameraIndex is { } index
                    ? _webcamSampler.SampleAsync(index, cancellationToken)
                    : Task.FromResult(new WebcamSample(null, null, "No camera detected"));
                var screenTask = _screenSampler.SampleAsync(cancellationToken, GetWindowBounds());
                await Task.WhenAll(cameraTask, screenTask);
                cancellationToken.ThrowIfCancellationRequested();

                var camera = await cameraTask;
                var screenLuminance = await screenTask;
                if (camera.Luminance is null && _cameraIndex is not null)
                {
                    _cameraIndex = null;
                    CameraStatus.Text = "Camera unavailable or permission denied; using remaining sensors.";
                }
                else
                {
                    CameraStatus.Text = camera.Luminance is { } value
                        ? $"Room light estimate · {value:0}% · camera exposure may affect the reading."
                        : "Camera unavailable; using remaining sensors.";
                }
                ScreenStatus.Text = screenLuminance is { } content
                    ? $"Screen average · {content:0}% luminance across displays."
                    : "Screen sampling unavailable; using remaining sensors.";

                double? screenRecommendation = screenLuminance is { } level ? SensorRecommendations.FromScreenLuminance(level) : null;
                var timePreference = SensorRecommendations.FromTimeOfDay(DateTime.Now);
                var decision = engine.Update(new SensorSnapshot(camera.Luminance, screenRecommendation, timePreference, DateTimeOffset.Now));
                if (firstSample || decision.Changed)
                {
                    var applied = await ApplyBrightnessAsync(decision.Target, camera.Luminance, screenLuminance, cancellationToken);
                    firstSample = false;
                    if (!applied)
                    {
                        StatusText.Text = "No supported display is currently available; automatic mode will retry.";
                        await Task.Delay(SamplingInterval, cancellationToken);
                        continue;
                    }
                }
                StatusText.Text = $"Automatic brightness is on · next sample in one minute · {decision.Reason}.";
                await Task.Delay(SamplingInterval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                StatusText.Text = "A sensor or display failed. Automatic mode will retry in one minute.";
                try { await Task.Delay(SamplingInterval, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            }
        }
    }

    private async Task<bool> ApplyBrightnessAsync(double brightness, double? ambientLight, double? screenLuminance, CancellationToken cancellationToken = default)
    {
        var count = await _brightnessController.SetAllAsync((byte)Math.Round(brightness), cancellationToken);
        if (count == 0)
        {
            StatusText.Text = "No supported internal display was changed.";
            return false;
        }
        BrightnessValue.Text = $"{brightness:0}%";
        BrightnessSlider.Value = brightness;
        await _settingsStore.SaveSettingsAsync(new UserSettings(brightness, BrightnessSlider.Minimum, BrightnessSlider.Maximum, Opacity, false, _cameraIndex ?? 0), cancellationToken);
        await _settingsStore.AppendActivityAsync(brightness, ambientLight, screenLuminance, cancellationToken);
        if (_automaticCancellation is null) StatusText.Text = $"Brightness applied to {count} internal display(s).";
        return true;
    }

    private async Task StopAutomaticAsync()
    {
        var cancellation = _automaticCancellation;
        var loop = _automaticLoop;
        _automaticCancellation = null;
        _automaticLoop = null;
        cancellation?.Cancel();
        if (loop is not null)
        {
            try { await loop; }
            catch (OperationCanceledException) { }
        }
        cancellation?.Dispose();
        if (_trayAutoItem is not null)
        {
            _trayAutoItem.Checked = false;
            _trayAutoItem.Text = "Start automatic brightness";
        }
        AutoMode.Content = "Automatic off";
        CameraStatus.Text = "Camera is off while automatic mode is off.";
        ScreenStatus.Text = "Screen is not sampled while automatic mode is off.";
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Opacity = Math.Clamp(e.NewValue / 100, 0.6, 1);
        if (OpacityValue is not null) OpacityValue.Text = $"{e.NewValue:0}%";
        if (_isLoaded) _ = SaveOpacityAsync();
    }

    private async Task SaveOpacityAsync()
    {
        try
        {
            var settings = await _settingsStore.LoadSettingsAsync();
            await _settingsStore.SaveSettingsAsync(settings with { WindowOpacity = Opacity, CameraIndex = _cameraIndex ?? settings.CameraIndex });
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void RestoreWindow()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private System.Drawing.Rectangle? GetWindowBounds()
    {
        var handle = new WindowInteropHelper(this).Handle;
        return handle != IntPtr.Zero && GetWindowRect(handle, out var rect)
            ? System.Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom)
            : null;
    }

    private void HideToTray(bool showNotification = false)
    {
        ShowInTaskbar = false;
        Hide();
        if (showNotification) _trayIcon?.ShowBalloonTip(1800, "SmartBrightness AI", "Still running in the notification area.", Forms.ToolTipIcon.Info);
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized) HideToTray();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray(showNotification: true);
            return;
        }
        _updateCancellation.Cancel();
        _trayIcon?.ContextMenuStrip?.Dispose();
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private async Task ExitAsync()
    {
        await StopAutomaticAsync();
        _allowClose = true;
        Close();
    }

    private void StartUpdateInstaller(string installerPath)
    {
        _allowClose = true;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            _allowClose = false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterApplicationRestart(string? commandLine, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
}
