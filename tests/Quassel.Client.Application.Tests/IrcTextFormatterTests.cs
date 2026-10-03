using Quassel.Client.Application.Text;
using Quassel.Client.Application.Dcc;

namespace Quassel.Client.Application.Tests;

public sealed class IrcTextFormatterTests
{
    [Fact]
    public void StylesToggleIndependently_AndResetRestoresDefaults()
    {
        var runs = IrcTextFormatter.Parse("\u0002bold\u001D both\u0002 italic\u001F under\u001E strike\u0011 mono\u0016 reverse\u000F plain");
        Assert.True(runs[0].Style.Bold);
        Assert.True(runs[1].Style.Bold && runs[1].Style.Italic);
        Assert.False(runs[2].Style.Bold);
        Assert.True(runs[2].Style.Italic);
        Assert.True(runs[3].Style.Underline);
        Assert.True(runs[4].Style.Strikethrough);
        Assert.True(runs[5].Style.Monospace);
        Assert.True(runs[6].Style.Reverse);
        Assert.Equal(new IrcTextStyle(), runs[^1].Style);
    }

    [Fact]
    public void ColorsSupportBackgroundsExtendedPaletteHexAndDefaults()
    {
        var runs = IrcTextFormatter.Parse("\u000304,02red\u000376pastel\u0004ABCDEF,123456hex\u000399,99normal");
        Assert.Equal("#FF0000", runs[0].Style.Foreground);
        Assert.Equal("#00007F", runs[0].Style.Background);
        Assert.Equal("#FF9C9C", runs[1].Style.Foreground);
        Assert.Equal("#00007F", runs[1].Style.Background);
        Assert.Equal("#ABCDEF", runs[2].Style.Foreground);
        Assert.Equal("#123456", runs[2].Style.Background);
        Assert.Null(runs[3].Style.Foreground);
        Assert.Null(runs[3].Style.Background);
    }

    [Theory]
    [InlineData("\u0003,hello", ",hello")]
    [InlineData("\u000304, hello", ", hello")]
    [InlineData("\u0004ABCxyz", "ABCxyz")]
    [InlineData("\u0004ABCDEF,12xyz", ",12xyz")]
    [InlineData("\u000304123", "123")]
    public void ColorParserDoesNotEatLiteralPunctuationOrInvalidHex(string input, string expected)
    {
        Assert.Equal(expected, IrcFormattingCleaner.Clean(input));
    }

    [Fact]
    public void EveryPossibleReadBoundaryPreservesTextAndFormatting()
    {
        const string input = "\u000303,14Town \u0002square\u0002\u000F! \u0004ABCDEF,123456hex\u0004, ok\nHP> ";
        var expected = Expand(IrcTextFormatter.Parse(input));
        for (var split = 0; split <= input.Length; split++)
        {
            var parser = new IrcTextFormatter();
            var actual = parser.Feed(input[..split]).Concat(parser.Feed(input[split..], complete: true));
            Assert.Equal(expected, Expand(actual));
        }
        var bytewise = new IrcTextFormatter();
        var runs = input.SelectMany(c => bytewise.Feed(c.ToString())).Concat(bytewise.Feed("", complete: true));
        Assert.Equal(expected, Expand(runs));
    }

    [Fact]
    public void DccKeepsStyleAcrossReadsAndRemovesTerminalCommands()
    {
        var cleaner = new DccTextCleaner();
        var runs = cleaner.CleanRuns("\u0003").Concat(cleaner.CleanRuns("0"))
            .Concat(cleaner.CleanRuns("3\u0002Town\x1B[31m square\u000F\r\nHP> ")).ToArray();
        Assert.Equal("Town square\nHP> ", string.Concat(runs.Select(run => run.Text)));
        Assert.True(runs[0].Style.Bold);
        Assert.Equal("#009300", runs[0].Style.Foreground);
        Assert.Equal(new IrcTextStyle(), runs[^1].Style);
    }

    private static (char, IrcTextStyle)[] Expand(IEnumerable<IrcTextRun> runs) =>
        runs.SelectMany(run => run.Text.Select(c => (c, run.Style))).ToArray();
}
