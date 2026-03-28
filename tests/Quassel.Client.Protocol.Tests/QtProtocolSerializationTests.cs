using System.Reflection;
using System.Text;
using Quassel.Client.Domain;
using Quassel.Client.Protocol;
using Quassel.Client.Protocol.Qt;

namespace Quassel.Client.Protocol.Tests;

public class QtProtocolSerializationTests
{
    [Fact]
    public void RegisterHandshake_AdvertisesExtendedFeaturesAndLongMessageIds()
    {
        var method = typeof(QuasselCoreClient).GetMethod("BuildRegisterClientHandshake", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(method!.Invoke(null, null));
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);

        for (var index = 0; index < values.Count; index += 2)
        {
            var key = Encoding.UTF8.GetString(Assert.IsType<byte[]>(values[index]));
            map[key] = values[index + 1];
        }

        Assert.Equal((uint)0x8000, Assert.IsType<uint>(map["Features"]));

        var featureList = Assert.IsAssignableFrom<IEnumerable<string>>(map["FeatureList"]).ToArray();
        Assert.Contains("ExtendedFeatures", featureList);
        Assert.Contains("LongMessageId", featureList);
    }

    [Fact]
    public void PackedMessage_UsesFourByteMsgIdsWhenLongIdsAreDisabled()
    {
        var payload = QtPayloadBuilder.BuildPackedMessage(1, false, new MsgId(-1));
        var reader = new QtBinaryReader(payload, false);

        Assert.Equal<uint>(2, reader.ReadUInt32());
        Assert.Equal(1, QtValueHelpers.AsInt(reader.ReadVariant()));
        Assert.Equal(new MsgId(-1), QtValueHelpers.AsMsgId(reader.ReadVariant()));
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void PackedMessage_UsesEightByteMsgIdsWhenLongIdsAreEnabled()
    {
        const long messageId = (long)int.MaxValue + 42;

        var payload = QtPayloadBuilder.BuildPackedMessage(1, true, new MsgId(messageId));
        var reader = new QtBinaryReader(payload, true);

        Assert.Equal<uint>(2, reader.ReadUInt32());
        Assert.Equal(1, QtValueHelpers.AsInt(reader.ReadVariant()));
        Assert.Equal(new MsgId(messageId), QtValueHelpers.AsMsgId(reader.ReadVariant()));
        Assert.Equal(0, reader.Remaining);
    }
}
