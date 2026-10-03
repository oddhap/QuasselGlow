using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace QuasselGlow.Appearance;

/// <summary>Reserves a unique pastel color pair per nickname for the whole app session.</summary>
public static class NickColorPalette
{
    private sealed record ColorPair(Color Light, Color Dark)
    {
        public IBrush LightBrush { get; } = new ImmutableSolidColorBrush(Light);
        public IBrush DarkBrush { get; } = new ImmutableSolidColorBrush(Dark);
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<string, ColorPair> Assignments = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<Color> UsedLight = [];
    private static readonly HashSet<Color> UsedDark = [];
    private static readonly ColorPair Fallback = new(Color.Parse("#927CB8"), Color.Parse("#C7B7ED"));

    public static IBrush Resolve(string? nick, bool isDarkMode)
    {
        if (string.IsNullOrWhiteSpace(nick)) return isDarkMode ? Fallback.DarkBrush : Fallback.LightBrush;

        lock (Gate)
        {
            if (!Assignments.TryGetValue(nick, out var pair))
            {
                pair = Allocate(nick);
                Assignments.Add(nick, pair);
                UsedLight.Add(pair.Light);
                UsedDark.Add(pair.Dark);
            }
            return isDarkMode ? pair.DarkBrush : pair.LightBrush;
        }
    }

    private static ColorPair Allocate(string nick)
    {
        var seed = Hash(nick);
        ColorPair? best = null;
        var bestDistance = -1;
        // Try several hues so a small channel gets visibly distinct colors as well
        // as unique RGB values. Avoid a quadratic scan for very large rosters.
        var candidates = Assignments.Count < 256 ? 16 : 1;
        for (var attempt = 0; candidates > 0; attempt++)
        {
            var value = Mix(unchecked(seed + (uint)attempt * 0x9E3779B9));
            var hue = (value & 0xFFFF) * (360.0 / 65536);
            var saturation = 0.30 + ((value >> 16) & 0xFF) * (0.20 / 255);
            var variation = ((value >> 24) & 0xFF) * (0.10 / 255);
            var light = new HslColor(1, hue, saturation, 0.50 + variation).ToRgb();
            var dark = new HslColor(1, hue, saturation, 0.75 + variation).ToRgb();
            if (UsedLight.Contains(light) || UsedDark.Contains(dark)) continue;

            candidates--;
            var distance = int.MaxValue;
            if (Assignments.Count < 256)
            {
                foreach (var existing in Assignments.Values)
                    distance = Math.Min(distance, Math.Min(Distance(light, existing.Light), Distance(dark, existing.Dark)));
            }
            if (distance > bestDistance)
            {
                best = new(light, dark);
                bestDistance = distance;
            }
            if (distance >= 900) break;
        }
        return best!;
    }

    private static int Distance(Color left, Color right)
    {
        var r = left.R - right.R;
        var g = left.G - right.G;
        var b = left.B - right.B;
        return r * r + g * g + b * b;
    }

    private static uint Hash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value.ToLowerInvariant()) hash = (hash ^ character) * 16777619;
            return hash;
        }
    }

    private static uint Mix(uint value)
    {
        unchecked
        {
            value = (value ^ (value >> 16)) * 0x7FEB352D;
            value = (value ^ (value >> 15)) * 0x846CA68B;
            return value ^ (value >> 16);
        }
    }
}
