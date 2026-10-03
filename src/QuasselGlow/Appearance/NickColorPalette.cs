using Avalonia.Media;

namespace QuasselGlow.Appearance;

/// <summary>
/// Assigns a stable pastel colour to each chat nickname so the same person keeps
/// the same colour across the whole session.
/// </summary>
public static class NickColorPalette
{
    private static readonly Color[] LightColors =
    [
        // Muted pastels retain definition against the light chat background.
        Color.Parse("#927CB8"),
        Color.Parse("#599D98"),
        Color.Parse("#C18192"),
        Color.Parse("#759BCD"),
        Color.Parse("#B19559"),
        Color.Parse("#83A16E"),
        Color.Parse("#B980AA"),
        Color.Parse("#6D9FB8"),
        Color.Parse("#BA906F"),
        Color.Parse("#69A38C"),
        Color.Parse("#A49A5A"),
        Color.Parse("#9785C4")
    ];

    private static readonly Color[] DarkColors =
    [
        Color.Parse("#C7B7ED"),
        Color.Parse("#A1DDD8"),
        Color.Parse("#F0B8C5"),
        Color.Parse("#B6CEF1"),
        Color.Parse("#EAD39C"),
        Color.Parse("#C1DDA8"),
        Color.Parse("#ECC0DF"),
        Color.Parse("#B3DBEF"),
        Color.Parse("#E6C3A5"),
        Color.Parse("#ADE0CC"),
        Color.Parse("#E2D8A4"),
        Color.Parse("#C6BDF0")
    ];

    public static IBrush Resolve(string? nick, bool isDarkMode)
    {
        var colors = isDarkMode ? DarkColors : LightColors;

        if (string.IsNullOrWhiteSpace(nick))
        {
            return new SolidColorBrush(colors[0]);
        }

        return new SolidColorBrush(colors[Hash(nick) % colors.Length]);
    }

    private static int Hash(string value)
    {
        // FNV-1a, case-insensitive so the colour does not jump when IRC casing changes.
        unchecked
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;
            var hash = offsetBasis;
            foreach (var character in value.ToLowerInvariant())
            {
                hash ^= character;
                hash *= prime;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
