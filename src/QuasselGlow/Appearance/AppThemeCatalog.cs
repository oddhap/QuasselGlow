using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace QuasselGlow.Appearance;

public static class AppThemeCatalog
{
    public const string DefaultThemeKey = "glow";
    public const string DefaultModeKey = "light";
    public const string DynamicWallpaperThemeKey = "dynamicWallpaper";

    public static IReadOnlyList<string> ThemeKeys { get; } =
    [
        "glow",
        DynamicWallpaperThemeKey,
        "macos",
        "windows7",
        "windows10",
        "windows11",
        "ubuntu",
        "cobalt",
        "slate",
        "frost",
        "aubergine",
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
            ["macos"] = CreateMacosPalette(isDark: false),
            ["windows7"] = CreateWindows7Palette(isDark: false),
            ["windows10"] = CreateWindows10Palette(isDark: false),
            ["windows11"] = CreateWindows11Palette(isDark: false),
            ["ubuntu"] = CreateUbuntuPalette(isDark: false),
            ["cobalt"] = CreateCobaltPalette(isDark: false),
            ["slate"] = CreateSlatePalette(isDark: false),
            ["frost"] = CreateFrostPalette(isDark: false),
            ["aubergine"] = CreateAuberginePalette(isDark: false),
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
            ["macos"] = CreateMacosPalette(isDark: true),
            ["windows7"] = CreateWindows7Palette(isDark: true),
            ["windows10"] = CreateWindows10Palette(isDark: true),
            ["windows11"] = CreateWindows11Palette(isDark: true),
            ["ubuntu"] = CreateUbuntuPalette(isDark: true),
            ["cobalt"] = CreateCobaltPalette(isDark: true),
            ["slate"] = CreateSlatePalette(isDark: true),
            ["frost"] = CreateFrostPalette(isDark: true),
            ["aubergine"] = CreateAuberginePalette(isDark: true),
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
        if (!string.IsNullOrWhiteSpace(themeKey))
        {
            var trimmed = themeKey.Trim();
            if (string.Equals(trimmed, DynamicWallpaperThemeKey, StringComparison.OrdinalIgnoreCase))
            {
                return DynamicWallpaperThemeKey;
            }

            if (LightPalettes.ContainsKey(trimmed))
            {
                return trimmed;
            }
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

    public static bool IsWallpaperMatchedTheme(string? themeKey)
    {
        return string.Equals(NormalizeThemeKey(themeKey), DynamicWallpaperThemeKey, StringComparison.OrdinalIgnoreCase);
    }

    public static AppThemePalette ResolvePalette(string? themeKey, string? modeKey, WallpaperThemeColors? wallpaperColors = null)
    {
        var theme = NormalizeThemeKey(themeKey);
        var palettes = IsDarkMode(modeKey) ? DarkPalettes : LightPalettes;

        if (string.Equals(theme, DynamicWallpaperThemeKey, StringComparison.OrdinalIgnoreCase))
        {
            return wallpaperColors is null
                ? palettes[DefaultThemeKey]
                : CreateWallpaperPalette(IsDarkMode(modeKey), wallpaperColors);
        }

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

    private static AppThemePalette CreateMacosPalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#151517",
                shellPanel: "#1C1C1E",
                shellPanelMuted: "#232326",
                shellBorder: "#3A3A3C",
                inkStrong: "#F5F5F7",
                inkSoft: "#A1A1A6",
                accentRust: "#FF6961",
                accentTeal: "#0A84FF",
                accentSky: "#25364A",
                accentSand: "#2C2C2E",
                backdropStart: "#111214",
                backdropMid: "#1A1B1F",
                backdropEnd: "#202329")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#F5F5F7",
                shellPanel: "#FFFFFF",
                shellPanelMuted: "#F2F2F7",
                shellBorder: "#D2D2D7",
                inkStrong: "#1D1D1F",
                inkSoft: "#6E6E73",
                accentRust: "#FF5F57",
                accentTeal: "#0A84FF",
                accentSky: "#EAF2FF",
                accentSand: "#F2F2F7",
                backdropStart: "#F7F7F9",
                backdropMid: "#F3F6FB",
                backdropEnd: "#EEF1F6");
    }

    private static AppThemePalette CreateWindows7Palette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#0F1725",
                shellPanel: "#142134",
                shellPanelMuted: "#101A2A",
                shellBorder: "#38516E",
                inkStrong: "#F5FAFF",
                inkSoft: "#B4C6DA",
                accentRust: "#E6B458",
                accentTeal: "#4B97E8",
                accentSky: "#1D344E",
                accentSand: "#31394B",
                backdropStart: "#0E1624",
                backdropMid: "#12345B",
                backdropEnd: "#2A3345")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#EEF4FB",
                shellPanel: "#FBFDFF",
                shellPanelMuted: "#DDE9F6",
                shellBorder: "#A7BDD2",
                inkStrong: "#20364E",
                inkSoft: "#617A92",
                accentRust: "#D6A13B",
                accentTeal: "#3A82D6",
                accentSky: "#DCEAF8",
                accentSand: "#F7E8C7",
                backdropStart: "#F5F9FE",
                backdropMid: "#DCEAF8",
                backdropEnd: "#F3EEDC");
    }

    private static AppThemePalette CreateCobaltPalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#101722",
                shellPanel: "#162131",
                shellPanelMuted: "#121B28",
                shellBorder: "#355072",
                inkStrong: "#F4F8FD",
                inkSoft: "#AABBD1",
                accentRust: "#F0B35C",
                accentTeal: "#69B3FF",
                accentSky: "#223850",
                accentSand: "#2B3140",
                backdropStart: "#101722",
                backdropMid: "#163860",
                backdropEnd: "#2A3244")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#F5F8FD",
                shellPanel: "#FFFFFF",
                shellPanelMuted: "#E8EFF8",
                shellBorder: "#AFC2DB",
                inkStrong: "#1E2D40",
                inkSoft: "#5F738A",
                accentRust: "#C98A37",
                accentTeal: "#2B78D6",
                accentSky: "#DDEAF9",
                accentSand: "#F5E8C9",
                backdropStart: "#F7FAFF",
                backdropMid: "#EAF2FB",
                backdropEnd: "#F3EEDC");
    }

    private static AppThemePalette CreateWindows10Palette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#111111",
                shellPanel: "#1B1B1B",
                shellPanelMuted: "#151515",
                shellBorder: "#303030",
                inkStrong: "#FAFAFA",
                inkSoft: "#B5B5B5",
                accentRust: "#E81123",
                accentTeal: "#0078D7",
                accentSky: "#1B2D40",
                accentSand: "#262626",
                backdropStart: "#101010",
                backdropMid: "#141C24",
                backdropEnd: "#181818")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#F2F2F2",
                shellPanel: "#FFFFFF",
                shellPanelMuted: "#F3F3F3",
                shellBorder: "#D9D9D9",
                inkStrong: "#1F1F1F",
                inkSoft: "#5F5F5F",
                accentRust: "#E81123",
                accentTeal: "#0078D7",
                accentSky: "#E5F1FB",
                accentSand: "#F3F3F3",
                backdropStart: "#F6F6F6",
                backdropMid: "#EEF3F7",
                backdropEnd: "#E8EEF4");
    }

    private static AppThemePalette CreateSlatePalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#101317",
                shellPanel: "#171C22",
                shellPanelMuted: "#12161B",
                shellBorder: "#2E3844",
                inkStrong: "#F5F7FA",
                inkSoft: "#A9B2BD",
                accentRust: "#F7630C",
                accentTeal: "#0078D4",
                accentSky: "#1A2C3F",
                accentSand: "#202B37",
                backdropStart: "#101317",
                backdropMid: "#112338",
                backdropEnd: "#18212A")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#F4F7FB",
                shellPanel: "#FFFFFF",
                shellPanelMuted: "#E9EEF5",
                shellBorder: "#C9D4DF",
                inkStrong: "#18222D",
                inkSoft: "#5A6877",
                accentRust: "#D83B01",
                accentTeal: "#0067C0",
                accentSky: "#DCEBF9",
                accentSand: "#EDF2F7",
                backdropStart: "#F5F8FB",
                backdropMid: "#EAF1F8",
                backdropEnd: "#E5EEF8");
    }

    private static AppThemePalette CreateWindows11Palette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#111318",
                shellPanel: "#1B1F26",
                shellPanelMuted: "#151920",
                shellBorder: "#313847",
                inkStrong: "#F5F7FA",
                inkSoft: "#AEB7C4",
                accentRust: "#E88A73",
                accentTeal: "#6EA8FF",
                accentSky: "#20283A",
                accentSand: "#20242C",
                backdropStart: "#111318",
                backdropMid: "#151C28",
                backdropEnd: "#1D2433")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#F3F5F9",
                shellPanel: "#FFFFFF",
                shellPanelMuted: "#F5F7FB",
                shellBorder: "#D9E2F0",
                inkStrong: "#1C1F26",
                inkSoft: "#6B7280",
                accentRust: "#E57D73",
                accentTeal: "#2563EB",
                accentSky: "#EAF1FF",
                accentSand: "#EEF2F8",
                backdropStart: "#F7F9FC",
                backdropMid: "#F0F4FB",
                backdropEnd: "#EDF2FF");
    }

    private static AppThemePalette CreateFrostPalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#0F1115",
                shellPanel: "#171A20",
                shellPanelMuted: "#12151A",
                shellBorder: "#2B313B",
                inkStrong: "#F5F7FB",
                inkSoft: "#AAB3C2",
                accentRust: "#F1A065",
                accentTeal: "#6F9BFF",
                accentSky: "#20273A",
                accentSand: "#212733",
                backdropStart: "#0F1115",
                backdropMid: "#171D29",
                backdropEnd: "#1C2432")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#F7F9FD",
                shellPanel: "#FFFFFF",
                shellPanelMuted: "#EFF3F9",
                shellBorder: "#D7DFEA",
                inkStrong: "#1A2230",
                inkSoft: "#626C7C",
                accentRust: "#E6845E",
                accentTeal: "#2F6FEB",
                accentSky: "#E8EEFF",
                accentSand: "#EEF3FB",
                backdropStart: "#F8FAFD",
                backdropMid: "#F1F5FB",
                backdropEnd: "#EEF1FF");
    }

    private static AppThemePalette CreateUbuntuPalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#1D1B22",
                shellPanel: "#26212E",
                shellPanelMuted: "#1F1B27",
                shellBorder: "#4B4056",
                inkStrong: "#F8F3F8",
                inkSoft: "#C7BBCB",
                accentRust: "#E95420",
                accentTeal: "#C061CB",
                accentSky: "#382529",
                accentSand: "#34263A",
                backdropStart: "#1A1720",
                backdropMid: "#261E2B",
                backdropEnd: "#2C2434")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#F8F5F5",
                shellPanel: "#FFFFFF",
                shellPanelMuted: "#F1ECEB",
                shellBorder: "#D6C7C2",
                inkStrong: "#2C1F26",
                inkSoft: "#6C5B64",
                accentRust: "#E95420",
                accentTeal: "#77216F",
                accentSky: "#F8E5DE",
                accentSand: "#F3E7F3",
                backdropStart: "#FBF7F6",
                backdropMid: "#F8EEE9",
                backdropEnd: "#F3E9F4");
    }

    private static AppThemePalette CreateAuberginePalette(bool isDark)
    {
        return isDark
            ? CreateDerivedPalette(
                isDark: true,
                shellBg: "#160F17",
                shellPanel: "#211722",
                shellPanelMuted: "#1A121B",
                shellBorder: "#493349",
                inkStrong: "#FAF1F8",
                inkSoft: "#C8B2C3",
                accentRust: "#FF6D3A",
                accentTeal: "#B774D0",
                accentSky: "#382128",
                accentSand: "#32203A",
                backdropStart: "#160F17",
                backdropMid: "#261426",
                backdropEnd: "#2C1C33")
            : CreateDerivedPalette(
                isDark: false,
                shellBg: "#FFF7F4",
                shellPanel: "#FFFCFB",
                shellPanelMuted: "#F7ECEB",
                shellBorder: "#E4D1CF",
                inkStrong: "#2E2027",
                inkSoft: "#7A6370",
                accentRust: "#E95420",
                accentTeal: "#77216F",
                accentSky: "#F8E7DF",
                accentSand: "#F4E6F2",
                backdropStart: "#FFF7F4",
                backdropMid: "#FFF0EA",
                backdropEnd: "#F7EDF8");
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

    private static AppThemePalette CreateWallpaperPalette(bool isDark, WallpaperThemeColors colors)
    {
        var primaryAccent = PrepareWallpaperAccent(colors.Primary, isDark);
        var secondaryAccent = PrepareWallpaperAccent(colors.Secondary, isDark);

        return isDark
            ? CreateDerivedPalette(
                true,
                Blend(C("#111820"), primaryAccent, 0.10),
                Blend(C("#18212B"), primaryAccent, 0.12),
                Blend(C("#131A22"), secondaryAccent, 0.10),
                Blend(C("#2A3643"), primaryAccent, 0.22),
                C("#F4F7FB"),
                Blend(C("#AAB8C6"), primaryAccent, 0.12),
                secondaryAccent,
                primaryAccent,
                Blend(C("#213142"), primaryAccent, 0.34),
                Blend(C("#3C3023"), secondaryAccent, 0.24),
                Blend(C("#10141A"), primaryAccent, 0.08),
                Blend(C("#16202C"), primaryAccent, 0.22),
                Blend(C("#261A18"), secondaryAccent, 0.18))
            : CreateDerivedPalette(
                false,
                Blend(C("#FFFDF9"), primaryAccent, 0.055),
                Blend(C("#FFFFFF"), primaryAccent, 0.025),
                Blend(C("#F3F0E8"), primaryAccent, 0.095),
                Blend(C("#D9D6CF"), primaryAccent, 0.18),
                C("#1F2937"),
                Blend(C("#526072"), primaryAccent, 0.10),
                secondaryAccent,
                primaryAccent,
                Blend(C("#DDE9F4"), primaryAccent, 0.28),
                Blend(C("#F9E8D1"), secondaryAccent, 0.24),
                Blend(C("#FFFDF9"), primaryAccent, 0.055),
                Blend(C("#F8FBFF"), primaryAccent, 0.16),
                Blend(C("#FFF1E6"), secondaryAccent, 0.16));
    }

    private static Color PrepareWallpaperAccent(Color color, bool isDark)
    {
        var luminance = GetRelativeLuminance(color);
        if (isDark)
        {
            var lifted = luminance < 0.45 ? Blend(color, Colors.White, 0.32) : color;
            return GetRelativeLuminance(lifted) > 0.82 ? Blend(lifted, Colors.Black, 0.16) : lifted;
        }

        var lowered = luminance > 0.62 ? Blend(color, Colors.Black, 0.34) : color;
        return GetRelativeLuminance(lowered) < 0.24 ? Blend(lowered, Colors.White, 0.18) : lowered;
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

        return CreateDerivedPalette(
            isDark,
            shellBgColor,
            shellPanelColor,
            shellPanelMutedColor,
            shellBorderColor,
            inkStrongColor,
            inkSoftColor,
            accentRustColor,
            accentTealColor,
            accentSkyColor,
            accentSandColor,
            backdropStartColor,
            backdropMidColor,
            backdropEndColor);
    }

    private static AppThemePalette CreateDerivedPalette(
        bool isDark,
        Color shellBgColor,
        Color shellPanelColor,
        Color shellPanelMutedColor,
        Color shellBorderColor,
        Color inkStrongColor,
        Color inkSoftColor,
        Color accentRustColor,
        Color accentTealColor,
        Color accentSkyColor,
        Color accentSandColor,
        Color backdropStartColor,
        Color backdropMidColor,
        Color backdropEndColor)
    {
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

    private static double GetRelativeLuminance(Color color)
    {
        static double Convert(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Convert(color.R)) + (0.7152 * Convert(color.G)) + (0.0722 * Convert(color.B));
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

public sealed class AppDisplayOption : ObservableObject
{
    private string _displayName;

    public AppDisplayOption(string key, string displayName)
    {
        Key = key;
        _displayName = displayName;
    }

    public string Key { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public override string ToString() => DisplayName;
}
