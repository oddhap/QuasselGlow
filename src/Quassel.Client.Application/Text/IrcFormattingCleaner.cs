namespace Quassel.Client.Application.Text;

public static class IrcFormattingCleaner
{
    public static string Clean(string? input, bool preserveCtcpDelimiter = false) =>
        string.Concat(IrcTextFormatter.Parse(input, preserveCtcpDelimiter).Select(run => run.Text));
}
