using System.Buffers.Binary;
using Quassel.Client.Domain;

namespace Quassel.Client.Protocol.Qt;

internal sealed class QtBinaryReader
{
    private readonly ReadOnlyMemory<byte> _buffer;
    private readonly bool _useLongMessageIds;

    public QtBinaryReader(ReadOnlyMemory<byte> buffer, bool useLongMessageIds = false)
    {
        _buffer = buffer;
        _useLongMessageIds = useLongMessageIds;
    }

    public int Position { get; private set; }
    public int Remaining => _buffer.Length - Position;

    public ReadOnlyMemory<byte> RemainingMemory() => _buffer[Position..];

    public byte ReadByte()
    {
        Ensure(1);
        return _buffer.Span[Position++];
    }

    public sbyte ReadSByte()
    {
        return unchecked((sbyte)ReadByte());
    }

    public ushort ReadUInt16()
    {
        Ensure(2);
        var value = BinaryPrimitives.ReadUInt16BigEndian(_buffer.Span[Position..]);
        Position += 2;
        return value;
    }

    public short ReadInt16()
    {
        Ensure(2);
        var value = BinaryPrimitives.ReadInt16BigEndian(_buffer.Span[Position..]);
        Position += 2;
        return value;
    }

    public uint ReadUInt32()
    {
        Ensure(4);
        var value = BinaryPrimitives.ReadUInt32BigEndian(_buffer.Span[Position..]);
        Position += 4;
        return value;
    }

    public int ReadInt32()
    {
        Ensure(4);
        var value = BinaryPrimitives.ReadInt32BigEndian(_buffer.Span[Position..]);
        Position += 4;
        return value;
    }

    public long ReadInt64()
    {
        Ensure(8);
        var value = BinaryPrimitives.ReadInt64BigEndian(_buffer.Span[Position..]);
        Position += 8;
        return value;
    }

    public ulong ReadUInt64()
    {
        Ensure(8);
        var value = BinaryPrimitives.ReadUInt64BigEndian(_buffer.Span[Position..]);
        Position += 8;
        return value;
    }

    public byte[] ReadByteArray()
    {
        var length = ReadUInt32();
        if (length == uint.MaxValue)
        {
            return Array.Empty<byte>();
        }

        Ensure((int)length);
        var value = _buffer.Span.Slice(Position, (int)length).ToArray();
        Position += (int)length;
        return value;
    }

    public string ReadQString()
    {
        var byteCount = ReadUInt32();
        if (byteCount == uint.MaxValue || byteCount == 0)
        {
            return string.Empty;
        }

        if ((byteCount & 1) != 0)
        {
            throw new InvalidDataException("Encountered a QString with an odd byte count.");
        }

        Ensure((int)byteCount);
        var text = QtValueHelpers.Utf16BigEndian.GetString(_buffer.Span.Slice(Position, (int)byteCount));
        Position += (int)byteCount;
        return text;
    }

    public List<string> ReadQStringList()
    {
        var count = ReadUInt32();
        var values = new List<string>((int)count);
        for (var index = 0; index < count; index++)
        {
            values.Add(ReadQString());
        }

        return values;
    }

    public List<object?> ReadVariantList()
    {
        var count = ReadUInt32();
        var values = new List<object?>((int)count);
        for (var index = 0; index < count; index++)
        {
            values.Add(ReadVariant());
        }

        return values;
    }

    public Dictionary<string, object?> ReadVariantMap()
    {
        var count = ReadUInt32();
        var values = new Dictionary<string, object?>((int)count, StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
        {
            values[ReadQString()] = ReadVariant();
        }

        return values;
    }

    public QuasselBufferInfo ReadBufferInfo()
    {
        var bufferId = new BufferId(ReadInt32());
        var networkId = new NetworkId(ReadInt32());
        var type = (QuasselBufferType)ReadInt16();
        var groupId = ReadUInt32();
        var bufferName = QtValueHelpers.Utf8.GetString(ReadByteArray());
        return new QuasselBufferInfo(bufferId, networkId, type, groupId, bufferName);
    }

    public QuasselMessage ReadMessage()
    {
        var messageId = new MsgId(_useLongMessageIds ? ReadInt64() : ReadInt32());
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(ReadUInt32());
        var type = (QuasselMessageType)ReadUInt32();
        var flags = (QuasselMessageFlags)ReadByte();
        var bufferInfo = ReadBufferInfo();
        var sender = QtValueHelpers.Utf8.GetString(ReadByteArray());
        var contents = QtValueHelpers.Utf8.GetString(ReadByteArray());

        return new QuasselMessage(messageId, timestamp, bufferInfo, type, contents, sender, flags);
    }

    public DateOnly ReadQDate()
    {
        var julianDay = (int)ReadUInt32();
        if (julianDay == 0)
        {
            return default;
        }

        return DateOnly.FromDayNumber(julianDay - 1721426);
    }

    public TimeSpan ReadQTime()
    {
        var millisecondsSinceStartOfDay = (int)ReadUInt32();
        return millisecondsSinceStartOfDay < 0
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(millisecondsSinceStartOfDay);
    }

    public DateTimeOffset ReadQDateTime()
    {
        var date = ReadQDate();
        var time = ReadQTime();
        var spec = ReadSByte();

        if (date == default)
        {
            return default;
        }

        var dateTime = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified).Add(time);
        return spec is 1 or 2 or 3
            ? new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc))
            : new DateTimeOffset(dateTime, TimeZoneInfo.Local.GetUtcOffset(dateTime));
    }

    public object? ReadVariant()
    {
        var type = (QtVariantType)ReadUInt32();
        _ = ReadSByte();

        return type switch
        {
            QtVariantType.Void => null,
            QtVariantType.Bool => ReadByte() != 0,
            QtVariantType.Int => ReadInt32(),
            QtVariantType.UInt => ReadUInt32(),
            QtVariantType.QChar => (char)ReadUInt16(),
            QtVariantType.QVariantMap => ReadVariantMap(),
            QtVariantType.QVariantList => ReadVariantList(),
            QtVariantType.QString => ReadQString(),
            QtVariantType.QStringList => ReadQStringList(),
            QtVariantType.QByteArray => ReadByteArray(),
            QtVariantType.Long => ReadInt64(),
            QtVariantType.Short => ReadInt16(),
            QtVariantType.Char => ReadSByte(),
            QtVariantType.ULong => ReadUInt64(),
            QtVariantType.UShort => ReadUInt16(),
            QtVariantType.UChar => ReadByte(),
            QtVariantType.QVariant => ReadVariant(),
            QtVariantType.UserType => ReadUserType(),
            QtVariantType.QDateTime => ReadQDateTime(),
            QtVariantType.QDate => ReadQDate(),
            QtVariantType.QTime => ReadQTime(),
            _ => throw new NotSupportedException($"Unsupported Qt variant type: {type}"),
        };
    }

    private object? ReadUserType()
    {
        var typeName = QtValueHelpers.Utf8.GetString(ReadByteArray()).TrimEnd('\0');
        return typeName switch
        {
            "BufferId" => new BufferId(ReadInt32()),
            "BufferInfo" => ReadBufferInfo(),
            "Identity" => ReadVariantMap(),
            "IdentityId" => new IdentityId(ReadInt32()),
            "Message" => ReadMessage(),
            "MsgId" => new MsgId(_useLongMessageIds ? ReadInt64() : ReadInt32()),
            "NetworkId" => new NetworkId(ReadInt32()),
            "NetworkInfo" => ReadVariantMap(),
            "Network::Server" => ReadVariantMap(),
            "PeerPtr" => ReadInt64(),
            _ => throw new NotSupportedException($"Unsupported Qt user type: {typeName}"),
        };
    }

    private void Ensure(int byteCount)
    {
        if (Position + byteCount > _buffer.Length)
        {
            throw new EndOfStreamException("Unexpected end of Qt data stream.");
        }
    }
}
