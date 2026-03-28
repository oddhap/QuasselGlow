namespace Quassel.Client.Domain;

public sealed record QuasselNetworkState(
    NetworkId NetworkId,
    string NetworkName,
    string CurrentServer,
    string MyNick,
    bool IsConnected,
    int Latency,
    int ConnectionState);
