namespace Quassel.Client.Protocol.Qt;

internal static class QtPayloadBuilder
{
    public static byte[] BuildHandshakeList(IReadOnlyList<object?> values)
    {
        var writer = new QtBinaryWriter();
        writer.WriteVariantList(values);
        return writer.ToArray();
    }

    public static byte[] BuildPackedMessage(short requestType, bool useLongMessageIds = false, params object?[] values)
    {
        var writer = new QtBinaryWriter(useLongMessageIds);
        writer.WriteUInt32((uint)(1 + values.Length));
        writer.WriteVariant(requestType);

        foreach (var value in values)
        {
            writer.WriteVariant(value);
        }

        return writer.ToArray();
    }

    public static byte[] BuildPackedMessageWithRawVariant(short requestType, ReadOnlySpan<byte> rawVariant, bool useLongMessageIds = false)
    {
        var writer = new QtBinaryWriter(useLongMessageIds);
        writer.WriteUInt32(2);
        writer.WriteVariant(requestType);
        writer.WriteRaw(rawVariant);
        return writer.ToArray();
    }

    public static Dictionary<string, object?> ReadHandshakeMap(ReadOnlyMemory<byte> payload)
    {
        var reader = new QtBinaryReader(payload);
        var values = reader.ReadVariantList();
        if (values.Count % 2 != 0)
        {
            throw new InvalidDataException("Handshake payload contained an uneven number of values.");
        }

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var index = 0; index < values.Count; index += 2)
        {
            result[QtValueHelpers.AsUtf8String(values[index])] = values[index + 1];
        }

        return result;
    }
}
