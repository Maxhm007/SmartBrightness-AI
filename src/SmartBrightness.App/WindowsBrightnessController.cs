using System.Management;

namespace SmartBrightness.App;

public sealed record DisplayCapability(string InstanceName, byte? CurrentBrightness, IReadOnlyList<byte> SupportedLevels);

/// <summary>Controls only internal displays exposed through the Windows WMI brightness provider.</summary>
public sealed class WindowsBrightnessController
{
    public Task<IReadOnlyList<DisplayCapability>> DetectAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<DisplayCapability>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var results = new List<DisplayCapability>();
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness");
            using var instances = searcher.Get();
            foreach (ManagementObject monitor in instances)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (monitor)
                {
                    var instanceName = monitor["InstanceName"]?.ToString() ?? "Internal display";
                    byte? current = monitor["CurrentBrightness"] is byte value ? value : null;
                    var levels = monitor["Level"] is byte[] bytes ? bytes : Array.Empty<byte>();
                    results.Add(new DisplayCapability(instanceName, current, levels));
                }
            }
            return results;
        }, cancellationToken);

    public Task<int> SetAllAsync(byte brightness, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = 0;
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            using var instances = searcher.Get();
            foreach (ManagementObject monitor in instances)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (monitor)
                {
                    using var parameters = new ManagementBaseObjectWrapper(monitor.GetMethodParameters("WmiSetBrightness"));
                    parameters.Value["Timeout"] = (uint)2;
                    parameters.Value["Brightness"] = brightness;
                    using var result = monitor.InvokeMethod("WmiSetBrightness", parameters.Value, null);
                    count++;
                }
            }
            return count;
        }, cancellationToken);

    private sealed class ManagementBaseObjectWrapper(ManagementBaseObject value) : IDisposable
    {
        public ManagementBaseObject Value { get; } = value;
        public void Dispose() => Value.Dispose();
    }
}
