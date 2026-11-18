using OpenCvSharp;

namespace SmartBrightness.App;

public sealed record WebcamSample(int? DeviceIndex, double? Luminance, string Status);

/// <summary>Opens a camera only for a sample and releases it before returning; image data is never persisted.</summary>
public sealed class WebcamSampler
{
    private const int MaximumProbeIndex = 7;

    public Task<int?> FindFirstAvailableAsync(int preferredDeviceIndex = 0, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var first = Math.Clamp(preferredDeviceIndex, 0, MaximumProbeIndex);
            var indexes = Enumerable.Range(0, MaximumProbeIndex + 1).OrderBy(index => index == first ? 0 : 1).ThenBy(index => Math.Abs(index - first));
            foreach (var index in indexes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var capture = new VideoCapture(index, VideoCaptureAPIs.MSMF);
                    if (!capture.IsOpened()) continue;
                    capture.Release();
                    return (int?)index;
                }
                catch (Exception ex) when (ex is OpenCvSharpException or DllNotFoundException or InvalidOperationException) { }
            }
            return null;
        }, cancellationToken);

    public Task<WebcamSample> SampleAsync(int deviceIndex, CancellationToken cancellationToken = default) =>
        Task.Run(() => Sample(deviceIndex, cancellationToken), cancellationToken);

    private static WebcamSample Sample(int deviceIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var capture = new VideoCapture(deviceIndex, VideoCaptureAPIs.MSMF);
            if (!capture.IsOpened()) return new(deviceIndex, null, "Camera unavailable or permission denied");

            capture.Set(VideoCaptureProperties.FrameWidth, 320);
            capture.Set(VideoCaptureProperties.FrameHeight, 240);
            using var frame = new Mat();
            if (!capture.Read(frame) || frame.Empty()) return new(deviceIndex, null, "Camera frame unavailable");

            using var small = new Mat();
            using var gray = new Mat();
            Cv2.Resize(frame, small, new OpenCvSharp.Size(64, 48), 0, 0, InterpolationFlags.Area);
            cancellationToken.ThrowIfCancellationRequested();
            Cv2.CvtColor(small, gray, ColorConversionCodes.BGR2GRAY);
            var mean = Cv2.Mean(gray).Val0;
            capture.Release();
            return new(deviceIndex, double.IsFinite(mean) ? Math.Clamp(mean / 255 * 100, 0, 100) : null, "Camera sampled; frame discarded");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is OpenCvSharpException or DllNotFoundException or InvalidOperationException)
        {
            return new(deviceIndex, null, "Camera unavailable or permission denied");
        }
    }
}
