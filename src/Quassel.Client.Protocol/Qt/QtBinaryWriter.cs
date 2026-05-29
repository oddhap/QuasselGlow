using System.Buffers.Binary;
using Quassel.Client.Domain;

namespace Quassel.Client.Protocol.Qt;

internal sealed class QtBinaryWriter
{
    private readonly MemoryStream _stream = new();
    private readonly bool _useLongMessageIds;

    public QtBinaryWriter(bool useLongMessageIds = false)
    {
        _useLongMessageIds = useLongMessageIds;
    }

    public byte[] ToArray() => _stream.ToArray();

    public void WriteRaw(ReadOnlySpan<byte> bytes)
    {
        _stream.Write(bytes);
    }

    public void WriteByte(byte value)
    {
        _stream.WriteByte(value);
    }

    public void WriteSByte(sbyte value)
    {
        _stream.WriteByte(unchecked((byte)value));
    }

    public void WriteUInt16(ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteInt16(short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteUInt32(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteInt32(int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteInt64(long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteUInt64(ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteByteArray(byte[]? value)
    {
        if (value is null)
        {
            WriteUInt32(uint.MaxValue);
            return;
        }

        WriteUInt32((uint)value.Length);
        _stream.Write(value, 0, value.Length);
    }

    public void WriteQString(string? value)
    {
        if (value is null)
        {
            WriteUInt32(uint.MaxValue);
            return;
        }

        if (value.Length == 0)
        {
            WriteUInt32(0);
            return;
        }

        var bytes = QtValueHelpers.Utf16BigEndian.GetBytes(value);
        WriteUInt32((uint)bytes.Length);
        _stream.Write(bytes, 0, bytes.Length);
    }

    public void WriteQStringList(IReadOnlyList<string> values)
    {
        WriteUInt32((uint)values.Count);
        foreach (var value in values)
        {
            WriteQString(value);
        }
    }

    public void WriteQDate(DateOnly value)
    {
        WriteUInt32(value == default ? 0 : (uint)(value.DayNumber + 1721426));
    }

    public void WriteQTime(TimeSpan value)
    {
        WriteUInt32((uint)value.TotalMilliseconds);
    }

    public void WriteQDateTime(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        WriteQDate(DateOnly.FromDateTime(utc.UtcDateTime));
        WriteQTime(utc.TimeOfDay);
        WriteSByte(1);
    }

    public void WriteVariantList(IReadOnlyList<object?> values)
    {
        WriteUInt32((uint)values.Count);
        foreach (var value in values)
        {
            WriteVariant(value);
        }
    }

    public void WriteVariantMap(IReadOnlyDictionary<string, object?> values)
    {
        WriteUInt32((uint)values.Count);
        foreach (var pair in values)
        {
            WriteQString(pair.Key);
            WriteVariant(pair.Value);
        }
    }

    public void WriteBufferInfo(QuasselBufferInfo bufferInfo)
    {
        WriteInt32(bufferInfo.BufferId.Value);
        WriteInt32(bufferInfo.NetworkId.Value);
        WriteInt16((short)bufferInfo.Type);
        WriteUInt32(bufferInfo.GroupId);
        WriteByteArray(QtValueHelpers.Utf8.GetBytes(bufferInfo.BufferName));
    }

    public void WriteVariant(object? value)
    {
        switch (value)
        {
            case null:
                WriteHeader(QtVariantType.Void, true);
                return;
            case bool boolean:
                WriteHeader(QtVariantType.Bool);
                WriteByte(boolean ? (byte)1 : (byte)0);
                return;
            case int number:
                WriteHeader(QtVariantType.Int);
                WriteInt32(number);
                return;
            case uint number:
                WriteHeader(QtVariantType.UInt);
                WriteUInt32(number);
                return;
            case short number:
                WriteHeader(QtVariantType.Short);
                WriteInt16(number);
                return;
            case ushort number:
                WriteHeader(QtVariantType.UShort);
                WriteUInt16(number);
                return;
            case sbyte number:
                WriteHeader(QtVariantType.Char);
                WriteSByte(number);
                return;
            case byte number:
                WriteHeader(QtVariantType.UChar);
                WriteByte(number);
                return;
            case long number:
                WriteHeader(QtVariantType.Long);
                WriteInt64(number);
                return;
            case ulong number:
                WriteHeader(QtVariantType.ULong);
                WriteUInt64(number);
                return;
            case string text:
                WriteHeader(QtVariantType.QString);
                WriteQString(text);
                return;
            case byte[] bytes:
                WriteHeader(QtVariantType.QByteArray);
                WriteByteArray(bytes);
                return;
            case DateTimeOffset dateTime:
                WriteHeader(QtVariantType.QDateTime);
                WriteQDateTime(dateTime);
                return;
            case IReadOnlyDictionary<string, object?> map:
                WriteHeader(QtVariantType.QVariantMap);
                WriteVariantMap(map);
                return;
            case IReadOnlyList<string> stringList:
                WriteHeader(QtVariantType.QStringList);
                WriteQStringList(stringList);
                return;
            case List<object?> list:
                WriteHeader(QtVariantType.QVariantList);
                WriteVariantList(list);
                return;
            case object?[] array:
                WriteHeader(QtVariantType.QVariantList);
                WriteVariantList(array);
                return;
            case BufferId bufferId:
                WriteCustomType("BufferId", writer => writer.WriteInt32(bufferId.Value));
                return;
            case NetworkId networkId:
                WriteCustomType("NetworkId", writer => writer.WriteInt32(networkId.Value));
                return;
            case IdentityId identityId:
                WriteCustomType("IdentityId", writer => writer.WriteInt32(identityId.Value));
                return;
            case MsgId messageId:
                WriteCustomType("MsgId", writer =>
                {
                    if (_useLongMessageIds)
                    {
                        writer.WriteInt64(messageId.Value);
                    }
                    else
                    {
                        writer.WriteInt32(unchecked((int)messageId.Value));
                    }
                });
                return;
            case QuasselBufferInfo bufferInfo:
                WriteCustomType("BufferInfo", writer => writer.WriteBufferInfo(bufferInfo));
                return;
            default:
                throw new NotSupportedException($"Unsupported Qt variant value: {value.GetType().FullName}");
        }
    }

    private void WriteCustomType(string typeName, Action<QtBinaryWriter> payloadWriter)
    {
        WriteHeader(QtVariantType.UserType);
        WriteByteArray(QtValueHelpers.Utf8.GetBytes(typeName + "\0"));
        payloadWriter(this);
    }

    private void WriteHeader(QtVariantType type, bool isNull = false)
    {
        WriteUInt32((uint)type);
        WriteSByte(isNull ? (sbyte)1 : (sbyte)0);
    }
}
