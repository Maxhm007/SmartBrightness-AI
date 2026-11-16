namespace SmartBrightness.Core;

public sealed record SensorSnapshot(
    double? AmbientLight,
    double? ScreenLuminance,
    double? TimePreference,
    DateTimeOffset Timestamp);

public sealed record BrightnessSettings(
    double Minimum = 10,
    double Maximum = 100,
    double WebcamWeight = 0.60,
    double ScreenWeight = 0.30,
    double TimeWeight = 0.10,
    double Smoothing = 0.25,
    double Hysteresis = 1.5,
    double Fallback = 50);

public sealed record BrightnessDecision(double Target, bool Changed, string Reason);

/// <summary>Combines normalized sensor recommendations into one bounded, smoothed target.</summary>
public sealed class BrightnessEngine
{
    private readonly BrightnessSettings _settings;
    private double? _smoothed;

    public BrightnessEngine(BrightnessSettings? settings = null)
    {
        _settings = settings ?? new BrightnessSettings();
        Validate(_settings);
    }

    public BrightnessDecision Update(SensorSnapshot sensors, double? manualOverride = null)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        if (manualOverride is { } manual)
        {
            var pinned = Clamp(manual);
            _smoothed = pinned;
            return new(pinned, true, "Manual override");
        }

        var weighted = new (double? Value, double Weight)[]
        {
            (FiniteOrNull(sensors.AmbientLight), _settings.WebcamWeight),
            (FiniteOrNull(sensors.ScreenLuminance), _settings.ScreenWeight),
            (FiniteOrNull(sensors.TimePreference), _settings.TimeWeight)
        };
        var totalWeight = weighted.Where(x => x.Value.HasValue).Sum(x => x.Weight);
        var raw = totalWeight <= 0
            ? Clamp(_settings.Fallback)
            : Clamp(weighted.Where(x => x.Value.HasValue).Sum(x => Normalize(x.Value!.Value) * x.Weight) / totalWeight);
        var next = _smoothed is null ? raw : _smoothed.Value + _settings.Smoothing * (raw - _smoothed.Value);
        var changed = _smoothed is null || Math.Abs(next - _smoothed.Value) >= _settings.Hysteresis;
        if (changed) _smoothed = next;
        return new(_smoothed ?? next, changed, totalWeight <= 0 ? "Sensor fallback" : "Sensor fusion");
    }

    private double Normalize(double value) => Clamp(value);

    private static double? FiniteOrNull(double? value) => value is { } number && double.IsFinite(number) ? number : null;

    private double Clamp(double value) => Math.Clamp(value, _settings.Minimum, _settings.Maximum);

    private static void Validate(BrightnessSettings settings)
    {
        if (!double.IsFinite(settings.Minimum) || !double.IsFinite(settings.Maximum) || settings.Minimum < 0 || settings.Maximum > 100 || settings.Minimum >= settings.Maximum)
            throw new ArgumentOutOfRangeException(nameof(settings), "Brightness limits must satisfy 0 <= minimum < maximum <= 100.");
        foreach (var weight in new[] { settings.WebcamWeight, settings.ScreenWeight, settings.TimeWeight })
            if (!double.IsFinite(weight) || weight < 0) throw new ArgumentOutOfRangeException(nameof(settings), "Sensor weights must be finite and non-negative.");
        if (!double.IsFinite(settings.Smoothing) || settings.Smoothing is <= 0 or > 1) throw new ArgumentOutOfRangeException(nameof(settings), "Smoothing must be in (0, 1].");
        if (!double.IsFinite(settings.Hysteresis) || settings.Hysteresis < 0) throw new ArgumentOutOfRangeException(nameof(settings), "Hysteresis must be non-negative.");
        if (!double.IsFinite(settings.Fallback)) throw new ArgumentOutOfRangeException(nameof(settings), "Fallback must be finite.");
    }
}
