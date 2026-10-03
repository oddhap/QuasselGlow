using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Quassel.Client.Domain;
using Quassel.Client.Application.Text;

namespace Quassel.Client.Application.Dcc;

public static class DccChatOfferParser
{
    public static DccChatOffer? Parse(QuasselMessage message)
    {
        if (message.IsSelf || message.Flags.HasFlag(QuasselMessageFlags.Ignored)
            || (message.Type & (QuasselMessageType.Plain | QuasselMessageType.Notice)) == 0)
        {
            return null;
        }

        var text = IrcFormattingCleaner.Clean(message.Contents, preserveCtcpDelimiter: true).Trim();
        // Inspect the original payload before IRC formatting replaces SOH with a glyph.
        if (text.StartsWith('\x01') && text.EndsWith('\x01') && text.Length >= 2)
        {
            text = text[1..^1];
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5
            || !parts[0].Equals("DCC", StringComparison.OrdinalIgnoreCase)
            || !parts[1].Equals("CHAT", StringComparison.OrdinalIgnoreCase)
            || !parts[2].Trim('"').Equals("chat", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
        {
            // Port zero is passive/reverse DCC, which needs a different handshake.
            return null;
        }

        IPAddress? address;
        if (parts[3].All(char.IsAsciiDigit))
        {
            if (!uint.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var numeric))
            {
                return null;
            }

            address = new IPAddress([(byte)(numeric >> 24), (byte)(numeric >> 16), (byte)(numeric >> 8), (byte)numeric]);
        }
        else if (!IPAddress.TryParse(parts[3], out address))
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.Broadcast) || address.IsIPv6Multicast
            || (address.AddressFamily == AddressFamily.InterNetwork && address.GetAddressBytes()[0] >= 224))
        {
            return null;
        }

        var nick = message.Sender.Split('!', 2)[0];
        return string.IsNullOrWhiteSpace(nick) ? null : new DccChatOffer(nick, address, port);
    }
}
