using Avalonia.Media;

namespace QuasselGlow.Appearance;

public static class AppThemeCatalog
{
    public const string DefaultThemeKey = "glow";
    public const string DefaultModeKey = "light";

    public static IReadOnlyList<string> ThemeKeys { get; } =
    [
        "glow",
        "tide",
        "ember",
        "aurora",
        "sage",
        "blossom",
        "lavender",
        "fjord",
        "citrus",
        "plum",
        "forest",
        "sandstone"
    ];

    public static IReadOnlyList<string> ModeKeys { get; } = ["light", "dark"];

    private static readonly IReadOnlyDictionary<string, AppThemePalette> LightPalettes =
        new Dictionary<string, AppThemePalette>(StringComparer.OrdinalIgnoreCase)
        {
            ["glow"] = CreateGlowPalette(isDark: false),
            ["tide"] = CreateTidePalette(isDark: false),
            ["ember"] = CreateEmberPalette(isDark: false),
            ["aurora"] = CreateAuroraPalette(isDark: false),
            ["sage"] = CreateSagePalette(isDark: false),
            ["blossom"] = CreateBlossomPalette(isDark: false),
            ["lavender"] = CreateLavenderPalette(isDark: false),
            ["fjord"] = CreateFjordPalette(isDark: false),
            ["citrus"] = CreateCitrusPalette(isDark: false),
            ["plum"] = CreatePlumPalette(isDark: false),
            ["forest"] = CreateForestPalette(isDark: false),
            ["sandstone"] = CreateSandstonePalette(isDark: false)
        };

    private static readonly IReadOnlyDictionary<string, AppThemePalette> DarkPalettes =
        new Dictionary<string, AppThemePalette>(StringComparer.OrdinalIgnoreCase)
        {
            ["glow"] = CreateGlowPalette(isDark: true),
            ["tide"] = CreateTidePalette(isDark: true),
            ["ember"] = CreateEmberPalette(isDark: true),
            ["aurora"] = CreateAuroraPalette(isDark: true),
            ["sage"] = CreateSagePalette(isDark: true),
            ["blossom"] = CreateBlossomPalette(isDark: true),
            ["lavender"] = CreateLavenderPalette(isDark: true),
            ["fjord"] = CreateFjordPalette(isDark: true),
            ["citrus"] = CreateCitrusPalette(isDark: true),
            ["plum"] = CreatePlumPalette(isDark: true),
            ["forest"] = CreateForestPalette(isDark: true),
            ["sandstone"] = CreateSandstonePalette(isDark: true)
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

    public static string GetThemeDisplayKey(string? themeKey)
    {
        return $"Theme{ToPascalCase(NormalizeThemeKey(themeKey))}";
    }

    public static string GetModeDisplayKey(string? modeKey)
    {
        return $"ThemeMode{ToPascalCase(NormalizeModeKey(modeKey))}";
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

    private static AppThemePalette CreateAuroraPalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#0D1220",
                shellPanel: "#151B2A",
                shellPanelMuted: "#111725",
                shellBorder: "#2C3550",
                inkStrong: "#F5F7FD",
                inkSoft: "#A8B4CB",
                accentRust: "#B49CFF",
                accentTeal: "#67D8BE",
                accentSky: "#22304B",
                accentSand: "#1B322D",
                backdropStart: "#0D1220",
                backdropMid: "#12213A",
                backdropEnd: "#231B35")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#F8FBFF",
                shellPanel: "#FCFEFF",
                shellPanelMuted: "#EDF3FA",
                shellBorder: "#D4DFEA",
                inkStrong: "#1D2635",
                inkSoft: "#64748A",
                accentRust: "#8B5CF6",
                accentTeal: "#0F9D84",
                accentSky: "#E6EDFF",
                accentSand: "#E6F6EF",
                backdropStart: "#F8FBFF",
                backdropMid: "#EEF7FF",
                backdropEnd: "#F4EEFF");
    }

    private static AppThemePalette CreateSagePalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#101612",
                shellPanel: "#172019",
                shellPanelMuted: "#121A14",
                shellBorder: "#2F4033",
                inkStrong: "#F3F8F1",
                inkSoft: "#AEBDAF",
                accentRust: "#7CC592",
                accentTeal: "#C1ACF4",
                accentSky: "#223128",
                accentSand: "#2D2638",
                backdropStart: "#101612",
                backdropMid: "#16251A",
                backdropEnd: "#241D2F")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#F7FBF7",
                shellPanel: "#FCFEFC",
                shellPanelMuted: "#EEF5EE",
                shellBorder: "#D3DED4",
                inkStrong: "#223027",
                inkSoft: "#66736B",
                accentRust: "#4D7758",
                accentTeal: "#705D99",
                accentSky: "#E8F1EC",
                accentSand: "#F1EBF8",
                backdropStart: "#F7FBF7",
                backdropMid: "#F0F7F1",
                backdropEnd: "#F6F0FA");
    }

    private static AppThemePalette CreateBlossomPalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#191118",
                shellPanel: "#241822",
                shellPanelMuted: "#1D1520",
                shellBorder: "#463145",
                inkStrong: "#FCF2F8",
                inkSoft: "#D8BCCC",
                accentRust: "#F08CB5",
                accentTeal: "#C79BFF",
                accentSky: "#38233A",
                accentSand: "#31233A",
                backdropStart: "#170F15",
                backdropMid: "#271626",
                backdropEnd: "#261A32")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#FFF8FA",
                shellPanel: "#FFFDFE",
                shellPanelMuted: "#F8EDF2",
                shellBorder: "#E6D5DE",
                inkStrong: "#34212C",
                inkSoft: "#83697A",
                accentRust: "#C45A87",
                accentTeal: "#8C4DB6",
                accentSky: "#FBE6EF",
                accentSand: "#F7EAF6",
                backdropStart: "#FFF8FA",
                backdropMid: "#FFF1F6",
                backdropEnd: "#FCF0FF");
    }

    private static AppThemePalette CreateLavenderPalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#131120",
                shellPanel: "#1B172A",
                shellPanelMuted: "#171321",
                shellBorder: "#342D47",
                inkStrong: "#F6F2FF",
                inkSoft: "#B7AED2",
                accentRust: "#B39AFF",
                accentTeal: "#8DB9FF",
                accentSky: "#2B2541",
                accentSand: "#1F2A42",
                backdropStart: "#12101E",
                backdropMid: "#1D1731",
                backdropEnd: "#1A2338")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#FAF8FF",
                shellPanel: "#FEFDFF",
                shellPanelMuted: "#F1ECF8",
                shellBorder: "#DBCFE8",
                inkStrong: "#2A2235",
                inkSoft: "#6E6481",
                accentRust: "#7C5CE0",
                accentTeal: "#406FBA",
                accentSky: "#ECE7FF",
                accentSand: "#E9F1FF",
                backdropStart: "#FAF8FF",
                backdropMid: "#F3EFFF",
                backdropEnd: "#EEF5FF");
    }

    private static AppThemePalette CreateFjordPalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#0D151B",
                shellPanel: "#142028",
                shellPanelMuted: "#101922",
                shellBorder: "#28414F",
                inkStrong: "#F3F8FC",
                inkSoft: "#A4BAC8",
                accentRust: "#5A9BC8",
                accentTeal: "#55D1B5",
                accentSky: "#1D3341",
                accentSand: "#18332C",
                backdropStart: "#0D151B",
                backdropMid: "#10242F",
                backdropEnd: "#12221B")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#F6FAFC",
                shellPanel: "#FCFEFF",
                shellPanelMuted: "#EDF4F7",
                shellBorder: "#D0DCE2",
                inkStrong: "#1B2832",
                inkSoft: "#5C7080",
                accentRust: "#2C6A8F",
                accentTeal: "#0B8A7A",
                accentSky: "#E0ECF5",
                accentSand: "#E1F4EF",
                backdropStart: "#F6FAFC",
                backdropMid: "#EDF6FB",
                backdropEnd: "#EBF4F0");
    }

    private static AppThemePalette CreateCitrusPalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#19150D",
                shellPanel: "#241F14",
                shellPanelMuted: "#1C180F",
                shellBorder: "#423927",
                inkStrong: "#FDF7EA",
                inkSoft: "#D3C7AB",
                accentRust: "#F2A93B",
                accentTeal: "#A5E05B",
                accentSky: "#3C3420",
                accentSand: "#24361B",
                backdropStart: "#19150D",
                backdropMid: "#2A2413",
                backdropEnd: "#1B2C15")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#FFFCF4",
                shellPanel: "#FFFFFA",
                shellPanelMuted: "#F8F3E6",
                shellBorder: "#E6DDC8",
                inkStrong: "#342C1E",
                inkSoft: "#7C725B",
                accentRust: "#A96200",
                accentTeal: "#4F7D1A",
                accentSky: "#FBF1C7",
                accentSand: "#EAF6D5",
                backdropStart: "#FFFCF4",
                backdropMid: "#FEF8E8",
                backdropEnd: "#F4FBE8");
    }

    private static AppThemePalette CreatePlumPalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#151017",
                shellPanel: "#1E1721",
                shellPanelMuted: "#18131A",
                shellBorder: "#3A2C40",
                inkStrong: "#F9F1FB",
                inkSoft: "#C9B8CF",
                accentRust: "#D784C7",
                accentTeal: "#65CACD",
                accentSky: "#33243A",
                accentSand: "#1C3334",
                backdropStart: "#140F16",
                backdropMid: "#251629",
                backdropEnd: "#132B2C")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#FBF7FC",
                shellPanel: "#FEFCFF",
                shellPanelMuted: "#F3EDF5",
                shellBorder: "#DCCFE0",
                inkStrong: "#2F2233",
                inkSoft: "#756679",
                accentRust: "#914E83",
                accentTeal: "#2F7F82",
                accentSky: "#F1E5F6",
                accentSand: "#E6F5F4",
                backdropStart: "#FBF7FC",
                backdropMid: "#F8F1FB",
                backdropEnd: "#F1FAFA");
    }

    private static AppThemePalette CreateForestPalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#0E1712",
                shellPanel: "#15211A",
                shellPanelMuted: "#101913",
                shellBorder: "#294235",
                inkStrong: "#F2FBF6",
                inkSoft: "#A9C0B4",
                accentRust: "#56B27B",
                accentTeal: "#60C9DB",
                accentSky: "#20382B",
                accentSand: "#1A3335",
                backdropStart: "#0E1712",
                backdropMid: "#11261C",
                backdropEnd: "#10242A")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#F5FAF7",
                shellPanel: "#FBFEFC",
                shellPanelMuted: "#EBF3EE",
                shellBorder: "#CEDBCC",
                inkStrong: "#1F2B24",
                inkSoft: "#5E7165",
                accentRust: "#1E6B4A",
                accentTeal: "#0E7992",
                accentSky: "#DFEFE5",
                accentSand: "#E1F4F1",
                backdropStart: "#F5FAF7",
                backdropMid: "#EDF6F0",
                backdropEnd: "#EAF8F7");
    }

    private static AppThemePalette CreateSandstonePalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#19120E",
                shellPanel: "#231A15",
                shellPanelMuted: "#1C1511",
                shellBorder: "#433228",
                inkStrong: "#FCF3ED",
                inkSoft: "#D3BDAF",
                accentRust: "#EB9A72",
                accentTeal: "#D7BC7A",
                accentSky: "#39281F",
                accentSand: "#352D1B",
                backdropStart: "#19120E",
                backdropMid: "#281C16",
                backdropEnd: "#2D2416")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#FDF8F2",
                shellPanel: "#FFFDFC",
                shellPanelMuted: "#F5EDE4",
                shellBorder: "#E2D4C6",
                inkStrong: "#33251D",
                inkSoft: "#7B665A",
                accentRust: "#B6653C",
                accentTeal: "#8C6F44",
                accentSky: "#F3E5D8",
                accentSand: "#F7EBD1",
                backdropStart: "#FDF8F2",
                backdropMid: "#FCF3EA",
                backdropEnd: "#FBF1DE");
    }

    private static AppThemePalette CreateDerivedPalette(
        bool isDark,
        string shellBg,
        string shellPanel,
        string shellPanelMuted,
        string shellBorder,
        string inkStrong,
        string inkSoft,
        string accentRust,
        string accentTeal,
        string accentSky,
        string accentSand,
        string backdropStart,
        string backdropMid,
        string backdropEnd)
    {
        var shellBgColor = C(shellBg);
        var shellPanelColor = C(shellPanel);
        var shellPanelMutedColor = C(shellPanelMuted);
        var shellBorderColor = C(shellBorder);
        var inkStrongColor = C(inkStrong);
        var inkSoftColor = C(inkSoft);
        var accentRustColor = C(accentRust);
        var accentTealColor = C(accentTeal);
        var accentSkyColor = C(accentSky);
        var accentSandColor = C(accentSand);
        var backdropStartColor = C(backdropStart);
        var backdropMidColor = C(backdropMid);
        var backdropEndColor = C(backdropEnd);

        return new AppThemePalette(
            ShellBg: shellBgColor,
            ShellPanel: shellPanelColor,
            ShellPanelMuted: shellPanelMutedColor,
            ShellBorder: shellBorderColor,
            InkStrong: inkStrongColor,
            InkSoft: inkSoftColor,
            AccentRust: accentRustColor,
            AccentTeal: accentTealColor,
            AccentSky: accentSkyColor,
            AccentSand: accentSandColor,
            ShellBackdropStart: backdropStartColor,
            ShellBackdropMid: backdropMidColor,
            ShellBackdropEnd: backdropEndColor,
            MessageRowBg: isDark
                ? Blend(shellPanelColor, inkStrongColor, 0.035)
                : Blend(shellPanelColor, shellBgColor, 0.1),
            MessageRowBorder: isDark
                ? Blend(shellBorderColor, inkStrongColor, 0.12)
                : Blend(shellBorderColor, shellPanelColor, 0.25),
            MessageRowSelfBg: Blend(shellPanelColor, accentTealColor, isDark ? 0.14 : 0.1),
            MessageRowSelfBorder: Blend(shellBorderColor, accentTealColor, isDark ? 0.55 : 0.35),
            MessageRowHighlightBg: Blend(shellPanelColor, accentRustColor, isDark ? 0.18 : 0.12),
            MessageRowHighlightBorder: Blend(shellBorderColor, accentRustColor, isDark ? 0.6 : 0.42),
            MessageRowStatusBg: isDark
                ? Blend(shellPanelMutedColor, shellPanelColor, 0.35)
                : Blend(shellPanelMutedColor, shellBgColor, 0.18),
            ComposerSelection: WithAlpha(accentTealColor, 0x6B),
            WindowChromeHoverBg: Blend(shellPanelMutedColor, accentSkyColor, isDark ? 0.2 : 0.22),
            WindowChromePressedBg: Blend(shellPanelMutedColor, shellBorderColor, isDark ? 0.48 : 0.35),
            WindowChromeCloseFg: Blend(inkSoftColor, accentRustColor, isDark ? 0.42 : 0.38),
            WindowChromeCloseHoverBg: Blend(shellPanelMutedColor, accentRustColor, isDark ? 0.22 : 0.14),
            WindowChromeCloseHoverFg: Blend(accentRustColor, inkStrongColor, isDark ? 0.16 : 0.1),
            WindowChromeClosePressedBg: Blend(shellPanelMutedColor, accentRustColor, isDark ? 0.34 : 0.25),
            WindowChromeClosePressedFg: Blend(accentRustColor, inkStrongColor, isDark ? 0.28 : 0.18),
            BufferPrefixBg: Blend(shellPanelColor, accentSkyColor, isDark ? 0.46 : 0.54));
    }

    private static Color C(string value) => Color.Parse(value);

    private static Color Blend(Color baseColor, Color tintColor, double tintAmount)
    {
        var amount = Math.Clamp(tintAmount, 0d, 1d);
        return Color.FromArgb(
            BlendChannel(baseColor.A, tintColor.A, amount),
            BlendChannel(baseColor.R, tintColor.R, amount),
            BlendChannel(baseColor.G, tintColor.G, amount),
            BlendChannel(baseColor.B, tintColor.B, amount));
    }

    private static byte BlendChannel(byte from, byte to, double amount)
    {
        return (byte)Math.Round(from + ((to - from) * amount));
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static string ToPascalCase(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : $"{char.ToUpperInvariant(value[0])}{value[1..]}";
    }
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
