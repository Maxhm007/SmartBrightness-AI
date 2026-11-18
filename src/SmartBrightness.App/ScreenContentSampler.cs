using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;

namespace SmartBrightness.App;

/// <summary>Samples a tiny in-memory thumbnail of each display. Pixel buffers are discarded after calculating a number.</summary>
public sealed class ScreenContentSampler
{
    private const int SampleWidth = 48;
    private const int SampleHeight = 36;
    private const uint SourceCopy = 0x00CC0020;
    private const uint DibRgbColors = 0;
    private const uint BiRgb = 0;
    private const int Halftone = 4;

    public Task<double?> SampleAsync(CancellationToken cancellationToken = default, Rectangle? excludedArea = null) =>
        Task.Run(() => Sample(cancellationToken, excludedArea), cancellationToken);

    private static double? Sample(CancellationToken cancellationToken, Rectangle? excludedArea)
    {
        try
        {
            var screens = Screen.AllScreens;
            if (screens.Length == 0) return null;
            double luminanceSum = 0;
            long pixelWeight = 0;

            foreach (var screen in screens)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rect = screen.Bounds;
                if (rect.Width <= 0 || rect.Height <= 0) continue;
                foreach (var region in Subtract(rect, excludedArea))
                {
                    var regionLuminance = SampleRegion(region.X, region.Y, region.Width, region.Height);
                    if (regionLuminance is not { } value) continue;
                    luminanceSum += value * (double)region.Width * region.Height;
                    pixelWeight += (long)region.Width * region.Height;
                }
            }

            return pixelWeight == 0 ? null : Math.Clamp(luminanceSum / pixelWeight, 0, 100);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static IEnumerable<Rectangle> Subtract(Rectangle source, Rectangle? excluded)
    {
        if (excluded is not { } cut || !source.IntersectsWith(cut))
        {
            yield return source;
            yield break;
        }
        var intersection = Rectangle.Intersect(source, cut);
        if (intersection.Height > 0 && intersection.Top > source.Top)
            yield return new Rectangle(source.Left, source.Top, source.Width, intersection.Top - source.Top);
        if (intersection.Height > 0 && intersection.Bottom < source.Bottom)
            yield return new Rectangle(source.Left, intersection.Bottom, source.Width, source.Bottom - intersection.Bottom);
        if (intersection.Width > 0 && intersection.Left > source.Left)
            yield return new Rectangle(source.Left, intersection.Top, intersection.Left - source.Left, intersection.Height);
        if (intersection.Width > 0 && intersection.Right < source.Right)
            yield return new Rectangle(intersection.Right, intersection.Top, source.Right - intersection.Right, intersection.Height);
    }

    private static double? SampleRegion(int x, int y, int width, int height)
    {
        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero) return null;
        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previous = IntPtr.Zero;
        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero) return null;
            var info = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = SampleWidth,
                    Height = -SampleHeight,
                    Planes = 1,
                    BitCount = 32,
                    Compression = BiRgb,
                    SizeImage = (uint)(SampleWidth * SampleHeight * 4)
                }
            };
            bitmap = CreateDIBSection(screenDc, ref info, DibRgbColors, out var bits, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero || bits == IntPtr.Zero) return null;
            previous = SelectObject(memoryDc, bitmap);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1)) return null;
            SetStretchBltMode(memoryDc, Halftone);
            if (!StretchBlt(memoryDc, 0, 0, SampleWidth, SampleHeight, screenDc, x, y, width, height, SourceCopy)) return null;

            var pixels = new byte[SampleWidth * SampleHeight * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            double sum = 0;
            var count = SampleWidth * SampleHeight;
            for (var i = 0; i < pixels.Length; i += 4)
                sum += pixels[i + 2] * 0.2126 + pixels[i + 1] * 0.7152 + pixels[i] * 0.0722;
            var percent = sum / count / 255 * 100;
            return double.IsFinite(percent) && percent > 0.1 ? Math.Clamp(percent, 0, 100) : null;
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(memoryDc, previous);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (memoryDc != IntPtr.Zero) DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern int SetStretchBltMode(IntPtr dc, int mode);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool StretchBlt(IntPtr destinationDc, int xDest, int yDest, int widthDest, int heightDest, IntPtr sourceDc, int xSource, int ySource, int widthSource, int heightSource, uint operation);
}
