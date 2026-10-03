using System.Text;

namespace Quassel.Client.Application.Text;

public sealed record IrcTextStyle(bool Bold = false, bool Italic = false, bool Underline = false,
    bool Strikethrough = false, bool Monospace = false, bool Reverse = false,
    string? Foreground = null, string? Background = null);

public sealed record IrcTextRun(string Text, IrcTextStyle Style);

/// <summary>Incremental IRC formatting parser. Color parameters may span network reads.</summary>
/// <remarks>Formatting and extended palette: https://modern.ircdocs.horse/formatting</remarks>
public sealed class IrcTextFormatter(bool showUnknownControls = true, bool preserveCtcpDelimiter = false)
{
    private IrcTextStyle _style = new();
    private string _pending = string.Empty;
    private static readonly string[] Colors = (
        "FFFFFF 000000 00007F 009300 FF0000 7F0000 9C009C FC7F00 FFFF00 00FC00 009393 00FFFF 0000FC FF00FF 7F7F7F D2D2D2 " +
        "470000 472100 474700 324700 004700 00472C 004747 002747 000047 2E0047 470047 47002A " +
        "740000 743A00 747400 517400 007400 007449 007474 004074 000074 4B0074 740074 740045 " +
        "B50000 B56300 B5B500 7DB500 00B500 00B571 00B5B5 0063B5 0000B5 7500B5 B500B5 B5006B " +
        "FF0000 FF8C00 FFFF00 B2FF00 00FF00 00FFA0 00FFFF 008CFF 0000FF A500FF FF00FF FF0098 " +
        "FF5959 FFB459 FFFF71 CFFF60 6FFF6F 65FFC9 6DFFFF 59B4FF 5959FF C459FF FF66FF FF59BC " +
        "FF9C9C FFD39C FFFF9C E2FF9C 9CFF9C 9CFFDB 9CFFFF 9CD3FF 9C9CFF DC9CFF FF9CFF FF94D3 " +
        "000000 131313 282828 363636 4D4D4D 656565 818181 9F9F9F BCBCBC E2E2E2 FFFFFF").Split(' ');

    public static IReadOnlyList<IrcTextRun> Parse(string? text, bool preserveCtcpDelimiter = false) =>
        new IrcTextFormatter(preserveCtcpDelimiter: preserveCtcpDelimiter).Feed(text ?? "", complete: true);

    public IReadOnlyList<IrcTextRun> Feed(string text, bool complete = false)
    {
        text = _pending + text;
        _pending = string.Empty;
        var runs = new List<IrcTextRun>();
        var buffer = new StringBuilder();
        void Flush()
        {
            if (buffer.Length == 0) return;
            runs.Add(new IrcTextRun(buffer.ToString(), _style));
            buffer.Clear();
        }
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '\x03' or '\x04')
            {
                Flush();
                if (!TryColor(text, i, c == '\x04', complete, out var end, out var fg, out var bg, out var hasForeground, out var hasBackground))
                {
                    _pending = text[i..];
                    break;
                }
                _style = hasForeground
                    ? _style with { Foreground = fg, Background = hasBackground ? bg : _style.Background }
                    : _style with { Foreground = null, Background = null };
                i = end - 1;
                continue;
            }
            if (c is '\x02' or '\x0F' or '\x11' or '\x12' or '\x16' or '\x1D' or '\x1E' or '\x1F')
            {
                Flush();
                _style = c switch
                {
                    '\x02' => _style with { Bold = !_style.Bold },
                    '\x1D' => _style with { Italic = !_style.Italic },
                    '\x1F' => _style with { Underline = !_style.Underline },
                    '\x1E' => _style with { Strikethrough = !_style.Strikethrough },
                    '\x11' => _style with { Monospace = !_style.Monospace },
                    '\x16' => _style with { Reverse = !_style.Reverse },
                    '\x12' => _style, // Legacy formatting control; never show as text.
                    _ => new IrcTextStyle()
                };
            }
            else if (c is '\r' or '\n' or '\t' || (c == '\x01' && preserveCtcpDelimiter))
            {
                buffer.Append(c);
                if (c == '\n') { Flush(); _style = new(); }
            }
            else if (!char.IsControl(c)) buffer.Append(c);
            else if (showUnknownControls && c < '\x20') buffer.Append((char)('\u2400' + c));
            else if (showUnknownControls && c == '\x7F') buffer.Append('\u2421');
        }
        Flush();
        return runs;
    }

    private static bool TryColor(string text, int start, bool hex, bool complete, out int end,
        out string? foreground, out string? background, out bool hasForeground, out bool hasBackground)
    {
        end = start + 1;
        foreground = background = null;
        hasForeground = hasBackground = false;
        var max = hex ? 6 : 2;
        bool Digit(char c) => hex ? char.IsAsciiHexDigit(c) : char.IsAsciiDigit(c);
        int Read(int index)
        {
            var first = index;
            while (index < text.Length && index - first < max && Digit(text[index])) index++;
            return index;
        }
        string? Color(int first, int last)
        {
            if (hex) return "#" + text[first..last];
            var number = int.Parse(text[first..last]);
            return number < Colors.Length ? "#" + Colors[number] : null;
        }
        var first = end;
        var last = Read(first);
        if (last == text.Length && !complete) return false;
        if (last == first || (hex && last - first != 6)) return true;
        hasForeground = true;
        foreground = Color(first, last);
        end = last;
        if (end >= text.Length || text[end] != ',') return true;
        first = end + 1;
        last = Read(first);
        if (last == text.Length && !complete) return false;
        // A comma without a valid background color is literal text.
        if (last == first || (hex && last - first != 6)) return true;
        hasBackground = true;
        background = Color(first, last);
        end = last;
        return true;
    }
}
