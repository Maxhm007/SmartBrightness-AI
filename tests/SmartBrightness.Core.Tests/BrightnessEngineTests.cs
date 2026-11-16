using SmartBrightness.Core;
using Xunit;

namespace SmartBrightness.Core.Tests;

public class BrightnessEngineTests
{
    private static SensorSnapshot Snapshot(double? ambient = null, double? screen = null, double? time = null) => new(ambient, screen, time, DateTimeOffset.UtcNow);

    [Fact]
    public void UsesFallbackWhenEverySensorIsUnavailable()
    {
        var result = new BrightnessEngine().Update(Snapshot());
        Assert.Equal(50, result.Target);
        Assert.Equal("Sensor fallback", result.Reason);
    }

    [Fact]
    public void RenormalizesWeightsWhenASensorIsUnavailable()
    {
        var result = new BrightnessEngine().Update(Snapshot(ambient: 80));
        Assert.Equal(80, result.Target);
    }

    [Fact]
    public void ManualOverrideTakesPriorityAndClampsToConfiguredRange()
    {
        var result = new BrightnessEngine(new BrightnessSettings(Minimum: 20, Maximum: 90)).Update(Snapshot(ambient: 10), 100);
        Assert.Equal(90, result.Target);
        Assert.Equal("Manual override", result.Reason);
    }

    [Fact]
    public void SmoothsLargeChangesAndSuppressesSmallSensorFluctuations()
    {
        var engine = new BrightnessEngine(new BrightnessSettings(Smoothing: .25, Hysteresis: 1.5));
        Assert.Equal(20, engine.Update(Snapshot(ambient: 20)).Target);
        var large = engine.Update(Snapshot(ambient: 80));
        Assert.Equal(35, large.Target);
        Assert.True(large.Changed);
        var small = engine.Update(Snapshot(ambient: 81));
        Assert.True(small.Changed);
        Assert.Equal(46.25, small.Target);

        var hysteresisEngine = new BrightnessEngine(new BrightnessSettings(Smoothing: 1, Hysteresis: 1.5));
        hysteresisEngine.Update(Snapshot(ambient: 20));
        Assert.Equal(80, hysteresisEngine.Update(Snapshot(ambient: 80)).Target);
        var fluctuation = hysteresisEngine.Update(Snapshot(ambient: 81));
        Assert.False(fluctuation.Changed);
        Assert.Equal(80, fluctuation.Target);
    }

    [Fact]
    public void RejectsInvalidLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BrightnessEngine(new BrightnessSettings(Minimum: 90, Maximum: 20)));
    }
}
