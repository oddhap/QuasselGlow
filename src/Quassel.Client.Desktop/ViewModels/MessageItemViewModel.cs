using Quassel.Client.Application.Text;
using Quassel.Client.Domain;

namespace Quassel.Client.Desktop.ViewModels;

public sealed class MessageItemViewModel : ViewModelBase
{
    public MessageItemViewModel(QuasselMessage message)
    {
        Model = message;
        SenderDisplay = ExtractNick(message.Sender);
        TimestampText = message.Timestamp.ToLocalTime().ToString("HH:mm");
        var cleanedContents = IrcFormattingCleaner.Clean(message.Contents);
        LineText = message.Type.HasFlag(QuasselMessageType.Action)
            ? $"* {SenderDisplay} {cleanedContents}"
            : cleanedContents;
    }

    public QuasselMessage Model { get; }
    public long MessageOrder => Model.MessageId.Value;
    public string TimestampText { get; }
    public string SenderDisplay { get; }
    public string LineText { get; }
    public bool IsSelf => Model.IsSelf;
    public bool IsHighlight => Model.IsHighlight;
    public bool IsStatus => Model.IsStatusMessage || Model.Type.HasFlag(QuasselMessageType.Info) || Model.Type.HasFlag(QuasselMessageType.Error);

    private static string ExtractNick(string sender)
    {
        if (string.IsNullOrWhiteSpace(sender))
        {
            return "core";
        }

        var bangIndex = sender.IndexOf('!');
        return bangIndex > 0 ? sender[..bangIndex] : sender;
    }
}
