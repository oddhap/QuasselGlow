using Quassel.Client.Application.Text;

namespace Quassel.Client.Application.Tests;

public sealed class IrcFormattingCleanerTests
{
    [Fact]
    public void Clean_RemovesFormattingCodesButKeepsNordicLetters()
    {
        const string input = "\u0002#homelien\u0002 - den offisielle kanalen for spørsmål og åpen hjelp";

        var actual = IrcFormattingCleaner.Clean(input);

        Assert.Equal("#homelien - den offisielle kanalen for spørsmål og åpen hjelp", actual);
    }

    [Fact]
    public void Clean_RemovesColorParameters()
    {
        const string input = "\u000304,02Viktig melding\u000F og \u00031,0fargekode";

        var actual = IrcFormattingCleaner.Clean(input);

        Assert.Equal("Viktig melding og fargekode", actual);
    }
}
