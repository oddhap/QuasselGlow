using System.Text;
using Quassel.Client.Domain;

namespace Quassel.Client.Protocol.Qt;

internal enum QtVariantType : uint
{
    Void = 0,
    Bool = 1,
    Int = 2,
    UInt = 3,
    QChar = 7,
    QVariantMap = 8,
    QVariantList = 9,
    QString = 10,
    QStringList = 11,
    QByteArray = 12,
    QDate = 14,
    QTime = 15,
    QDateTime = 16,
    Long = 129,
    Short = 130,
    Char = 131,
    ULong = 132,
    UShort = 133,
    UChar = 134,
    QVariant = 138,
    UserType = 127,
}

internal static class QtValueHelpers
{
    public static readonly UTF8Encoding Utf8 = new(false);
    public static readonly Encoding Utf16BigEndian = Encoding.BigEndianUnicode;

    public static string AsUtf8String(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            byte[] bytes => Utf8.GetString(bytes),
            _ => Convert.ToString(value) ?? string.Empty,
        };
    }

    public static string AsString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            byte[] bytes => Utf8.GetString(bytes),
            _ => Convert.ToString(value) ?? string.Empty,
        };
    }

    public static bool AsBool(object? value)
    {
        return value switch
        {
            bool boolean => boolean,
            byte number => number != 0,
            sbyte number => number != 0,
            short number => number != 0,
            ushort number => number != 0,
            int number => number != 0,
            uint number => number != 0,
            long number => number != 0,
            ulong number => number != 0,
            _ => false,
        };
    }

    public static int AsInt(object? value)
    {
        return value switch
        {
            int number => number,
            short number => number,
            ushort number => number,
            byte number => number,
            sbyte number => number,
            uint number => unchecked((int)number),
            long number => unchecked((int)number),
            BufferId id => id.Value,
            NetworkId id => id.Value,
            IdentityId id => id.Value,
            _ => 0,
        };
    }

    public static Dictionary<string, object?> AsMap(object? value)
    {
        return value as Dictionary<string, object?> ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public static List<object?> AsList(object? value)
    {
        return value as List<object?> ?? new List<object?>();
    }

    public static List<string> AsStringList(object? value)
    {
        return value switch
        {
            List<string> list => list,
            IReadOnlyList<string> list => list.ToList(),
            List<object?> list => list.Select(AsString).ToList(),
            _ => new List<string>()
        };
    }

    public static BufferId AsBufferId(object? value)
    {
        return value switch
        {
            BufferId bufferId => bufferId,
            int number => new BufferId(number),
            _ => new BufferId(0),
        };
    }

    public static NetworkId AsNetworkId(object? value)
    {
        return value switch
        {
            NetworkId networkId => networkId,
            int number => new NetworkId(number),
            _ => new NetworkId(0),
        };
    }

    public static MsgId AsMsgId(object? value)
    {
        return value switch
        {
            MsgId messageId => messageId,
            long number => new MsgId(number),
            int number => new MsgId(number),
            _ => new MsgId(0),
        };
    }

    public static QuasselBufferInfo AsBufferInfo(object? value)
    {
        if (value is QuasselBufferInfo info)
        {
            return info;
        }

        throw new InvalidDataException("Expected a BufferInfo value.");
    }

    public static QuasselMessage AsMessage(object? value)
    {
        if (value is QuasselMessage message)
        {
            return message;
        }

        throw new InvalidDataException("Expected a Message value.");
    }
}
