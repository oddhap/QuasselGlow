using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Quassel.Client.Application.Text;

namespace QuasselGlow.ViewModels;

public sealed class MessageTextSegment(string text, string? url = null, IrcTextStyle? style = null)
{
    public string Text { get; } = text;
    public string? Url { get; } = url;
    public IrcTextStyle Style { get; } = style ?? new();
    public bool IsLink => !string.IsNullOrWhiteSpace(Url);

    private static readonly Regex LinkRegex = new(
        @"(?<url>(?:https?://|www\.)[^\s<>""]+[^\s<>"".,;:!?])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ReadOnlyCollection<MessageTextSegment> FromRuns(IReadOnlyList<IrcTextRun> runs)
    {
        var text = string.Concat(runs.Select(run => run.Text));
        var links = LinkRegex.Matches(text).Cast<Match>().ToArray();
        var result = new List<MessageTextSegment>();
        var start = 0;
        foreach (var run in runs)
        {
            var end = start + run.Text.Length;
            var position = start;
            foreach (var link in links.Where(link => link.Index < end && link.Index + link.Length > start))
            {
                var linkStart = Math.Max(start, link.Index);
                var linkEnd = Math.Min(end, link.Index + link.Length);
                if (linkStart > position) result.Add(new(text[position..linkStart], style: run.Style));
                var url = link.Value.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + link.Value : link.Value;
                result.Add(new(text[linkStart..linkEnd], url, run.Style));
                position = linkEnd;
            }
            if (position < end) result.Add(new(text[position..end], style: run.Style));
            start = end;
        }
        return result.AsReadOnly();
    }
}
