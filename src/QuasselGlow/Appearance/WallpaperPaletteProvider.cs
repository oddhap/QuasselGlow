using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace QuasselGlow.Appearance;

public sealed class WallpaperPaletteProvider
{
    private const int SampleWidth = 72;
    private const int ProcessTimeoutMs = 2000;

    private string? _cachedPath;
    private DateTime _cachedLastWriteTimeUtc;
    private WallpaperThemeColors? _cachedColors;

    public WallpaperThemeColors? TryGetThemeColors()
    {
        var path = TryGetWallpaperPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            if (string.Equals(_cachedPath, path, StringComparison.Ordinal)
                && _cachedLastWriteTimeUtc == lastWriteTimeUtc)
            {
                return _cachedColors;
            }

            var colors = ExtractThemeColors(path);
            _cachedPath = path;
            _cachedLastWriteTimeUtc = lastWriteTimeUtc;
            _cachedColors = colors;
            return colors;
        }
        catch
        {
            return null;
        }
    }

    public static WallpaperThemeColors? SelectThemeColors(IEnumerable<Color> colors)
    {
        var buckets = new Dictionary<int, ColorBucket>();
        foreach (var color in colors)
        {
            if (color.A < 192)
            {
                continue;
            }

            var saturation = GetSaturation(color);
            var luminance = GetPerceptualLuminance(color);
            if (saturation < 0.14 || luminance < 0.08 || luminance > 0.94)
            {
                continue;
            }

            var key = Quantize(color);
            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new ColorBucket();
                buckets[key] = bucket;
            }

            bucket.Add(color, 0.6 + saturation);
        }

        var ranked = buckets.Values
            .Select(bucket => bucket.ToColor())
            .OrderByDescending(candidate => candidate.Score)
            .ToArray();

        if (ranked.Length == 0)
        {
            return null;
        }

        var primary = ranked[0].Color;
        var secondary = ranked
            .Skip(1)
            .FirstOrDefault(candidate => GetColorDistance(primary, candidate.Color) > 72)
            .Color;

        if (secondary.A == 0)
        {
            secondary = ranked.Length > 1 ? ranked[1].Color : primary;
        }

        return new WallpaperThemeColors(primary, secondary);
    }

    private static WallpaperThemeColors? ExtractThemeColors(string path)
    {
        using var stream = File.OpenRead(path);
        using var bitmap = Bitmap.DecodeToWidth(stream, SampleWidth, BitmapInterpolationMode.LowQuality);
        var pixelSize = bitmap.PixelSize;
        if (pixelSize.Width <= 0 || pixelSize.Height <= 0)
        {
            return null;
        }

        var stride = pixelSize.Width * 4;
        var pixels = new byte[stride * pixelSize.Height];
        using (var framebuffer = new MemoryLockedFramebuffer(pixels, pixelSize, stride))
        {
            bitmap.CopyPixels(framebuffer);
        }

        return SelectThemeColors(ReadBgraPixels(pixels, pixelSize, stride));
    }

    private static IEnumerable<Color> ReadBgraPixels(byte[] pixels, PixelSize pixelSize, int stride)
    {
        for (var y = 0; y < pixelSize.Height; y++)
        {
            var rowOffset = y * stride;
            for (var x = 0; x < pixelSize.Width; x++)
            {
                var offset = rowOffset + (x * 4);
                yield return Color.FromArgb(
                    pixels[offset + 3],
                    pixels[offset + 2],
                    pixels[offset + 1],
                    pixels[offset]);
            }
        }
    }

    private static string? TryGetWallpaperPath()
    {
        return OperatingSystem.IsMacOS() ? TryGetMacWallpaperPath() : null;
    }

    private static string? TryGetMacWallpaperPath()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                ArgumentList =
                {
                    "-e",
                    "tell application \"System Events\" to get picture of current desktop"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return null;
            }

            if (!process.WaitForExit(ProcessTimeoutMs))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                return null;
            }

            if (process.ExitCode != 0)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }

    private static int Quantize(Color color)
    {
        return ((color.R >> 3) << 10) | ((color.G >> 3) << 5) | (color.B >> 3);
    }

    private static double GetSaturation(Color color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        return max == 0 ? 0 : (max - min) / max;
    }

    private static double GetPerceptualLuminance(Color color)
    {
        return ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255d;
    }

    private static double GetColorDistance(Color left, Color right)
    {
        var red = left.R - right.R;
        var green = left.G - right.G;
        var blue = left.B - right.B;
        return Math.Sqrt((red * red) + (green * green) + (blue * blue));
    }

    private sealed class ColorBucket
    {
        private double _red;
        private double _green;
        private double _blue;
        private double _weight;
        private int _count;

        public void Add(Color color, double weight)
        {
            _red += color.R * weight;
            _green += color.G * weight;
            _blue += color.B * weight;
            _weight += weight;
            _count++;
        }

        public ColorCandidate ToColor()
        {
            if (_weight <= 0)
            {
                return new ColorCandidate(Colors.Transparent, 0);
            }

            var color = Color.FromRgb(
                (byte)Math.Clamp(Math.Round(_red / _weight), 0, 255),
                (byte)Math.Clamp(Math.Round(_green / _weight), 0, 255),
                (byte)Math.Clamp(Math.Round(_blue / _weight), 0, 255));

            return new ColorCandidate(color, _count * (0.7 + GetSaturation(color)));
        }
    }

    private readonly record struct ColorCandidate(Color Color, double Score);

    private sealed class MemoryLockedFramebuffer : ILockedFramebuffer
    {
        private readonly GCHandle _handle;

        public MemoryLockedFramebuffer(byte[] pixels, PixelSize size, int rowBytes)
        {
            _handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            Address = _handle.AddrOfPinnedObject();
            Size = size;
            RowBytes = rowBytes;
        }

        public IntPtr Address { get; }
        public PixelSize Size { get; }
        public int RowBytes { get; }
        public Vector Dpi { get; } = new(96, 96);
        public PixelFormat Format { get; } = PixelFormats.Bgra8888;
        public AlphaFormat AlphaFormat { get; } = AlphaFormat.Unpremul;

        public void Dispose()
        {
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }
    }
}

public sealed record WallpaperThemeColors(Color Primary, Color Secondary);
