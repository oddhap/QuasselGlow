namespace Quassel.Client.Domain;

public sealed record QuasselChannelTopicUpdate(
    NetworkId NetworkId,
    string ChannelName,
    string Topic);
