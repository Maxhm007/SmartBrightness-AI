using System.Text.Json;
using System.IO;

namespace SmartBrightness.App;

public sealed record UserSettings(double Brightness = 50, double Minimum = 10, double Maximum = 100, bool AutomaticEnabled = false);
public sealed record NumericActivityEntry(DateTimeOffset Timestamp, double Brightness);

public sealed class LocalSettingsStore
{
    private readonly string _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartBrightnessAI");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<UserSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await LoadAsync<UserSettings>("settings.json", cancellationToken).ConfigureAwait(false) ?? new UserSettings();
        var minimum = double.IsFinite(settings.Minimum) ? Math.Clamp(settings.Minimum, 0, 99) : 10;
        var maximum = double.IsFinite(settings.Maximum) ? Math.Clamp(settings.Maximum, minimum + 1, 100) : 100;
        var brightness = double.IsFinite(settings.Brightness) ? Math.Clamp(settings.Brightness, minimum, maximum) : 50;
        return new UserSettings(brightness, minimum, maximum, false);
    }

    public Task SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default) =>
        SaveAsync("settings.json", settings with { AutomaticEnabled = false }, cancellationToken);

    public async Task AppendActivityAsync(double brightness, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(brightness)) return;
        var history = await LoadAsync<List<NumericActivityEntry>>("history.json", cancellationToken).ConfigureAwait(false) ?? [];
        history.Add(new NumericActivityEntry(DateTimeOffset.Now, Math.Clamp(brightness, 0, 100)));
        if (history.Count > 500) history.RemoveRange(0, history.Count - 500);
        await SaveAsync("history.json", history, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T?> LoadAsync<T>(string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var path = Path.Combine(_folder, fileName);
            if (!File.Exists(path)) return default;
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException) { return default; }
        catch (IOException) { return default; }
    }

    private async Task SaveAsync<T>(string fileName, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, fileName);
        var temporaryPath = path + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, path, overwrite: true);
    }
}
