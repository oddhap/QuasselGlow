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
        Color.Parse("#7B6BD9"),
        Color.Parse("#0E7C7B"),
        Color.Parse("#9C4F5E"),
        Color.Parse("#4F6FA8"),
        Color.Parse("#A9761A"),
        Color.Parse("#5C7A3F"),
        Color.Parse("#A8557F"),
        Color.Parse("#4E7EA8"),
        Color.Parse("#8A5A2B"),
        Color.Parse("#3F7D6B"),
        Color.Parse("#8A6E0B"),
        Color.Parse("#6A5ACD")
    ];

    private static readonly Color[] DarkColors =
    [
        Color.Parse("#B4A6F0"),
        Color.Parse("#6FD8D4"),
        Color.Parse("#E8A0AC"),
        Color.Parse("#9DB8E8"),
        Color.Parse("#E0B94B"),
        Color.Parse("#A8CC8A"),
        Color.Parse("#E8A6CF"),
        Color.Parse("#96C8E8"),
        Color.Parse("#D9AE7C"),
        Color.Parse("#7FD1BC"),
        Color.Parse("#D6C56A"),
        Color.Parse("#B0A8F2")
    ];

    public static IBrush Resolve(string? nick, bool isSelf, bool isDarkMode)
    {
        var colors = isDarkMode ? DarkColors : LightColors;

        if (isSelf)
        {
            return new SolidColorBrush(colors[0]);
        }

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
