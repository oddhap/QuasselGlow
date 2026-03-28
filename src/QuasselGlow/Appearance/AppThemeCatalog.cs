using Avalonia.Media;

namespace QuasselGlow.Appearance;

public static class AppThemeCatalog
{
    public const string DefaultThemeKey = "glow";
    public const string DefaultModeKey = "light";

    public static IReadOnlyList<string> ThemeKeys { get; } = ["glow", "tide", "ember"];
    public static IReadOnlyList<string> ModeKeys { get; } = ["light", "dark"];

    private static readonly IReadOnlyDictionary<string, AppThemePalette> LightPalettes =
        new Dictionary<string, AppThemePalette>(StringComparer.OrdinalIgnoreCase)
        {
            ["glow"] = CreateGlowPalette(isDark: false),
            ["tide"] = CreateTidePalette(isDark: false),
            ["ember"] = CreateEmberPalette(isDark: false)
        };

    private static readonly IReadOnlyDictionary<string, AppThemePalette> DarkPalettes =
        new Dictionary<string, AppThemePalette>(StringComparer.OrdinalIgnoreCase)
        {
            ["glow"] = CreateGlowPalette(isDark: true),
            ["tide"] = CreateTidePalette(isDark: true),
            ["ember"] = CreateEmberPalette(isDark: true)
        };

    public static string NormalizeThemeKey(string? themeKey)
    {
        if (!string.IsNullOrWhiteSpace(themeKey) && LightPalettes.ContainsKey(themeKey.Trim()))
        {
            return themeKey.Trim();
        }

        return DefaultThemeKey;
    }

    public static string NormalizeModeKey(string? modeKey)
    {
        if (string.Equals(modeKey?.Trim(), "dark", StringComparison.OrdinalIgnoreCase))
        {
            return "dark";
        }

        return DefaultModeKey;
    }

    public static bool IsDarkMode(string? modeKey)
    {
        return string.Equals(NormalizeModeKey(modeKey), "dark", StringComparison.OrdinalIgnoreCase);
    }

    public static AppThemePalette ResolvePalette(string? themeKey, string? modeKey)
    {
        var theme = NormalizeThemeKey(themeKey);
        var palettes = IsDarkMode(modeKey) ? DarkPalettes : LightPalettes;
        return palettes.TryGetValue(theme, out var palette)
            ? palette
            : palettes[DefaultThemeKey];
    }

    private static AppThemePalette CreateGlowPalette(bool isDark)
    {
        return isDark
            ? new AppThemePalette(
                ShellBg: C("#12161C"),
                ShellPanel: C("#19202A"),
                ShellPanelMuted: C("#141A22"),
                ShellBorder: C("#2A3643"),
                InkStrong: C("#F4F7FB"),
                InkSoft: C("#AAB8C6"),
                AccentRust: C("#FF8A57"),
                AccentTeal: C("#5CD0C0"),
                AccentSky: C("#213142"),
                AccentSand: C("#3C3023"),
                ShellBackdropStart: C("#10141A"),
                ShellBackdropMid: C("#16202C"),
                ShellBackdropEnd: C("#261A18"),
                MessageRowBg: C("#1A212B"),
                MessageRowBorder: C("#293744"),
                MessageRowSelfBg: C("#142622"),
                MessageRowSelfBorder: C("#2C6A5F"),
                MessageRowHighlightBg: C("#3C2D17"),
                MessageRowHighlightBorder: C("#B9843E"),
                MessageRowStatusBg: C("#1A1821"),
                ComposerSelection: C("#6B5CD0C0"),
                WindowChromeHoverBg: C("#253140"),
                WindowChromePressedBg: C("#1D2733"),
                WindowChromeCloseFg: C("#E3B9A8"),
                WindowChromeCloseHoverBg: C("#3A231F"),
                WindowChromeCloseHoverFg: C("#FFBEA2"),
                WindowChromeClosePressedBg: C("#4A2A24"),
                WindowChromeClosePressedFg: C("#FFD3C2"),
                BufferPrefixBg: C("#22303D"))
            : new AppThemePalette(
                ShellBg: C("#FFF8F0"),
                ShellPanel: C("#FFFDF9"),
                ShellPanelMuted: C("#F3F0E8"),
                ShellBorder: C("#D9D6CF"),
                InkStrong: C("#1F2937"),
                InkSoft: C("#526072"),
                AccentRust: C("#D6652F"),
                AccentTeal: C("#0F766E"),
                AccentSky: C("#DDE9F4"),
                AccentSand: C("#F9E8D1"),
                ShellBackdropStart: C("#FFF8F0"),
                ShellBackdropMid: C("#F8FBFF"),
                ShellBackdropEnd: C("#FFF1E6"),
                MessageRowBg: C("#FFFDF9"),
                MessageRowBorder: C("#ECE7DF"),
                MessageRowSelfBg: C("#F4FBF8"),
                MessageRowSelfBorder: C("#A7D9C3"),
                MessageRowHighlightBg: C("#FFF2D8"),
                MessageRowHighlightBorder: C("#E7B978"),
                MessageRowStatusBg: C("#F3F0E8"),
                ComposerSelection: C("#6BD6ECE8"),
                WindowChromeHoverBg: C("#F6F1E9"),
                WindowChromePressedBg: C("#EEE4D7"),
                WindowChromeCloseFg: C("#9D7563"),
                WindowChromeCloseHoverBg: C("#F7E8E0"),
                WindowChromeCloseHoverFg: C("#8B5438"),
                WindowChromeClosePressedBg: C("#EEDACF"),
                WindowChromeClosePressedFg: C("#74412A"),
                BufferPrefixBg: C("#E8EDF4"));
    }

    private static AppThemePalette CreateTidePalette(bool isDark)
    {
        return isDark
            ? new AppThemePalette(
                ShellBg: C("#101721"),
                ShellPanel: C("#16202C"),
                ShellPanelMuted: C("#111A25"),
                ShellBorder: C("#2B3B4C"),
                InkStrong: C("#F1F7FF"),
                InkSoft: C("#A8BED1"),
                AccentRust: C("#FF9070"),
                AccentTeal: C("#61CBE7"),
                AccentSky: C("#21384C"),
                AccentSand: C("#2E3645"),
                ShellBackdropStart: C("#0F1721"),
                ShellBackdropMid: C("#13293B"),
                ShellBackdropEnd: C("#1F202E"),
                MessageRowBg: C("#192432"),
                MessageRowBorder: C("#2E4558"),
                MessageRowSelfBg: C("#13252F"),
                MessageRowSelfBorder: C("#2F6B7A"),
                MessageRowHighlightBg: C("#3A2A25"),
                MessageRowHighlightBorder: C("#C48366"),
                MessageRowStatusBg: C("#171E29"),
                ComposerSelection: C("#6B61CBE7"),
                WindowChromeHoverBg: C("#243547"),
                WindowChromePressedBg: C("#1B2937"),
                WindowChromeCloseFg: C("#F2B9A7"),
                WindowChromeCloseHoverBg: C("#402520"),
                WindowChromeCloseHoverFg: C("#FFC7B3"),
                WindowChromeClosePressedBg: C("#512C24"),
                WindowChromeClosePressedFg: C("#FFE0D4"),
                BufferPrefixBg: C("#223647"))
            : new AppThemePalette(
                ShellBg: C("#F7FBFF"),
                ShellPanel: C("#FCFEFF"),
                ShellPanelMuted: C("#EEF5FA"),
                ShellBorder: C("#CEDBE7"),
                InkStrong: C("#172230"),
                InkSoft: C("#55687B"),
                AccentRust: C("#D06A4C"),
                AccentTeal: C("#157C9B"),
                AccentSky: C("#DDEFFC"),
                AccentSand: C("#E9EEF6"),
                ShellBackdropStart: C("#F7FBFF"),
                ShellBackdropMid: C("#EEF6FF"),
                ShellBackdropEnd: C("#F6F0FF"),
                MessageRowBg: C("#FCFEFF"),
                MessageRowBorder: C("#E2EAF2"),
                MessageRowSelfBg: C("#F0FAFD"),
                MessageRowSelfBorder: C("#A8D6E3"),
                MessageRowHighlightBg: C("#FFF0E6"),
                MessageRowHighlightBorder: C("#E3B089"),
                MessageRowStatusBg: C("#EEF5FA"),
                ComposerSelection: C("#6B61CBE7"),
                WindowChromeHoverBg: C("#EDF4FA"),
                WindowChromePressedBg: C("#DFEAF3"),
                WindowChromeCloseFg: C("#8C6C61"),
                WindowChromeCloseHoverBg: C("#F8EAE5"),
                WindowChromeCloseHoverFg: C("#7F4D39"),
                WindowChromeClosePressedBg: C("#EFDACE"),
                WindowChromeClosePressedFg: C("#6F3E28"),
                BufferPrefixBg: C("#E3EEF8"));
    }

    private static AppThemePalette CreateEmberPalette(bool isDark)
    {
        return isDark
            ? new AppThemePalette(
                ShellBg: C("#171312"),
                ShellPanel: C("#221A19"),
                ShellPanelMuted: C("#1A1515"),
                ShellBorder: C("#3A2C2A"),
                InkStrong: C("#FBF3EF"),
                InkSoft: C("#D2BEB4"),
                AccentRust: C("#FF8F5A"),
                AccentTeal: C("#E0B94B"),
                AccentSky: C("#372824"),
                AccentSand: C("#332721"),
                ShellBackdropStart: C("#160F0F"),
                ShellBackdropMid: C("#231818"),
                ShellBackdropEnd: C("#2F2419"),
                MessageRowBg: C("#241D1C"),
                MessageRowBorder: C("#423130"),
                MessageRowSelfBg: C("#2A211C"),
                MessageRowSelfBorder: C("#7F633D"),
                MessageRowHighlightBg: C("#422715"),
                MessageRowHighlightBorder: C("#DE9554"),
                MessageRowStatusBg: C("#211919"),
                ComposerSelection: C("#6BE0B94B"),
                WindowChromeHoverBg: C("#362826"),
                WindowChromePressedBg: C("#2A201E"),
                WindowChromeCloseFg: C("#F0C0A7"),
                WindowChromeCloseHoverBg: C("#452521"),
                WindowChromeCloseHoverFg: C("#FFD2B7"),
                WindowChromeClosePressedBg: C("#572E26"),
                WindowChromeClosePressedFg: C("#FFE2D1"),
                BufferPrefixBg: C("#3A2C28"))
            : new AppThemePalette(
                ShellBg: C("#FFF6F0"),
                ShellPanel: C("#FFFDFB"),
                ShellPanelMuted: C("#F6ECE6"),
                ShellBorder: C("#E4D3CB"),
                InkStrong: C("#2B2222"),
                InkSoft: C("#6E5A56"),
                AccentRust: C("#CF6132"),
                AccentTeal: C("#A87A12"),
                AccentSky: C("#F5E4DE"),
                AccentSand: C("#F9E7D7"),
                ShellBackdropStart: C("#FFF6F0"),
                ShellBackdropMid: C("#FFF0EA"),
                ShellBackdropEnd: C("#FFF6DE"),
                MessageRowBg: C("#FFFDFB"),
                MessageRowBorder: C("#EEDFD7"),
                MessageRowSelfBg: C("#FFF5E8"),
                MessageRowSelfBorder: C("#E2C799"),
                MessageRowHighlightBg: C("#FFE6D2"),
                MessageRowHighlightBorder: C("#E5A46C"),
                MessageRowStatusBg: C("#F6ECE6"),
                ComposerSelection: C("#6BE0B94B"),
                WindowChromeHoverBg: C("#F8ECE8"),
                WindowChromePressedBg: C("#F0DFD7"),
                WindowChromeCloseFg: C("#9A6F61"),
                WindowChromeCloseHoverBg: C("#FAE7E0"),
                WindowChromeCloseHoverFg: C("#8D4E36"),
                WindowChromeClosePressedBg: C("#F0D6C7"),
                WindowChromeClosePressedFg: C("#753A20"),
                BufferPrefixBg: C("#F3E5DD"));
    }

    private static Color C(string value) => Color.Parse(value);
}

public sealed record AppThemePalette(
    Color ShellBg,
    Color ShellPanel,
    Color ShellPanelMuted,
    Color ShellBorder,
    Color InkStrong,
    Color InkSoft,
    Color AccentRust,
    Color AccentTeal,
    Color AccentSky,
    Color AccentSand,
    Color ShellBackdropStart,
    Color ShellBackdropMid,
    Color ShellBackdropEnd,
    Color MessageRowBg,
    Color MessageRowBorder,
    Color MessageRowSelfBg,
    Color MessageRowSelfBorder,
    Color MessageRowHighlightBg,
    Color MessageRowHighlightBorder,
    Color MessageRowStatusBg,
    Color ComposerSelection,
    Color WindowChromeHoverBg,
    Color WindowChromePressedBg,
    Color WindowChromeCloseFg,
    Color WindowChromeCloseHoverBg,
    Color WindowChromeCloseHoverFg,
    Color WindowChromeClosePressedBg,
    Color WindowChromeClosePressedFg,
    Color BufferPrefixBg);

public sealed record AppDisplayOption(string Key, string DisplayName)
{
    public override string ToString() => DisplayName;
}
