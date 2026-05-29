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

    [Fact]
    public void PackedMessage_CanCarryUtcDateTimeForHeartbeat()
    {
        var timestamp = DateTimeOffset.Parse("2026-05-29T11:30:00+02:00");

        var payload = QtPayloadBuilder.BuildPackedMessage(5, false, timestamp);
        var reader = new QtBinaryReader(payload, false);

        Assert.Equal<uint>(2, reader.ReadUInt32());
        Assert.Equal(5, QtValueHelpers.AsInt(reader.ReadVariant()));
        Assert.Equal(timestamp.ToUniversalTime(), Assert.IsType<DateTimeOffset>(reader.ReadVariant()));
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public async Task HeartBeatReply_ResetsMissedHeartBeats()
    {
        var client = new QuasselCoreClient(TimeSpan.FromMilliseconds(5), maxMissedHeartBeats: 1);
        var missedField = typeof(QuasselCoreClient).GetField("_missedHeartBeats", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(missedField);
        missedField!.SetValue(client, 1);

        var payload = QtPayloadBuilder.BuildPackedMessage(6, false, DateTimeOffset.UtcNow);
        var method = typeof(QuasselCoreClient).GetMethod("HandlePackedMessageAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(client, [new ReadOnlyMemory<byte>(payload), CancellationToken.None]));
        await task;

        Assert.Equal(0, Assert.IsType<int>(missedField.GetValue(client)));
    }

    [Fact]
    public async Task HeartBeatLoop_MarksConnectionLostAfterMissingReplies()
    {
        var client = new QuasselCoreClient(TimeSpan.FromMilliseconds(5), maxMissedHeartBeats: 1);
        var streamField = typeof(QuasselCoreClient).GetField("_stream", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(streamField);
        streamField!.SetValue(client, new MemoryStream());

        var states = new List<(QuasselConnectionState State, string? Message)>();
        client.ConnectionStateChanged += (state, message) => states.Add((state, message));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var method = typeof(QuasselCoreClient).GetMethod("HeartBeatLoopAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(client, [cts.Token]));
        await task;

        var finalState = Assert.Single(states);
        Assert.Equal(QuasselConnectionState.Error, finalState.State);
        Assert.Equal("Lost connection to Quassel core: no heartbeat reply received.", finalState.Message);
        Assert.False(client.IsConnected);
    }
}
