using System.Text;
using Quassel.Client.Application.Text;

namespace Quassel.Client.Application.Dcc;

/// <summary>Removes terminal commands while preserving MUD text, maps and split UTF-8 reads.</summary>
public sealed class DccTextCleaner
{
    private enum EscapeState { Text, Escape, Csi, String, StringEscape }
    private EscapeState _state;
    private readonly IrcTextFormatter _formatter = new(showUnknownControls: false);

    public string Clean(string text) => string.Concat(CleanRuns(text).Select(run => run.Text));

    public IReadOnlyList<IrcTextRun> CleanRuns(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            switch (_state)
            {
                case EscapeState.Escape:
                    _state = character switch
                    {
                        '[' => EscapeState.Csi,
                        ']' or 'P' or '^' or '_' => EscapeState.String,
                        _ => EscapeState.Text
                    };
                    break;
                case EscapeState.Csi:
                    if (character is >= '@' and <= '~')
                    {
                        _state = EscapeState.Text;
                    }
                    break;
                case EscapeState.String:
                    if (character == '\a') _state = EscapeState.Text;
                    else if (character == '\x1B') _state = EscapeState.StringEscape;
                    break;
                case EscapeState.StringEscape:
                    _state = character == '\\' ? EscapeState.Text : EscapeState.String;
                    break;
                default:
                    if (character == '\x1B') _state = EscapeState.Escape;
                    else if (character is '\n' or '\t' or '\x02' or '\x03' or '\x04' or '\x0F' or '\x11' or '\x12' or '\x16' or '\x1D' or '\x1E' or '\x1F' || !char.IsControl(character)) result.Append(character);
                    break;
            }
        }

        return _formatter.Feed(result.ToString());
    }
}
