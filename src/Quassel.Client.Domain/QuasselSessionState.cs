namespace Quassel.Client.Domain;

public sealed record QuasselSessionState(
    IReadOnlyList<object?> Identities,
    IReadOnlyList<QuasselBufferInfo> Buffers,
    IReadOnlyList<NetworkId> Networks);
