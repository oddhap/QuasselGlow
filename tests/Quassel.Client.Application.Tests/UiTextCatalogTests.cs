using Quassel.Client.Desktop.Localization;

namespace Quassel.Client.Application.Tests;

public sealed class UiTextCatalogTests
{
    private static readonly string[] ExpectedCodes =
    [
        "cs",
        "da",
        "de",
        "el",
        "en_GB",
        "en_US",
        "eo",
        "es",
        "et",
        "fi",
        "fr",
        "gl",
        "hi",
        "hu",
        "it",
        "ja",
        "ko",
        "lt",
        "mr",
        "nb",
        "nl",
        "oc",
        "pa",
        "pl",
        "pt",
        "pt_BR",
        "ro",
        "ru",
        "sl",
        "sq",
        "sr",
        "sv",
        "tr",
        "uk",
        "zh_CN"
    ];

    [Fact]
    public void SupportedLanguages_MatchesOfficialQuasselLanguageList()
    {
        var actualCodes = UiTextCatalog.Instance.SupportedLanguages.Select(option => option.Code).ToArray();
        Assert.Equal(ExpectedCodes, actualCodes);
    }

    [Fact]
    public void EverySupportedLanguage_ProvidesConnectionLabel()
    {
        var catalog = UiTextCatalog.Instance;

        foreach (var code in ExpectedCodes)
        {
            catalog.SetLanguage(code);
            Assert.False(string.IsNullOrWhiteSpace(catalog["Connection"]));
        }

        catalog.SetLanguage(UiTextCatalog.DefaultLanguageCode);
    }

    [Theory]
    [InlineData("nb-NO", "nb")]
    [InlineData("en-GB", "en_GB")]
    [InlineData("en-AU", "en_US")]
    [InlineData("pt-BR", "pt_BR")]
    [InlineData("zh-Hans-CN", "zh_CN")]
    [InlineData("no", "nb")]
    public void ResolveLanguageCode_NormalizesCommonLocales(string input, string expected)
    {
        Assert.Equal(expected, UiTextCatalog.ResolveLanguageCode(input));
    }
}
