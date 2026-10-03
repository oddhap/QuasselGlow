using System.Text;

namespace Quassel.Client.Application.Dcc;

/// <summary>Removes terminal commands while preserving MUD text, maps and split UTF-8 reads.</summary>
public sealed class DccTextCleaner
{
    private enum EscapeState { Text, Escape, Csi, String, StringEscape }
    private EscapeState _state;

    public string Clean(string text)
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
                    else if (character is '\n' or '\t' || !char.IsControl(character)) result.Append(character);
                    break;
            }
        }

        return result.ToString();
    }
}
