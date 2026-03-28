namespace Quassel.Client.Domain;

public sealed record QuasselChannelUser(
    string Nick,
    string Modes);

public sealed record QuasselChannelState(
    NetworkId NetworkId,
    string ChannelName,
    string Topic,
    IReadOnlyList<QuasselChannelUser> Users);
