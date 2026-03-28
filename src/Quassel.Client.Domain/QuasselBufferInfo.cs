namespace Quassel.Client.Domain;

public sealed record QuasselBufferInfo(
    BufferId BufferId,
    NetworkId NetworkId,
    QuasselBufferType Type,
    uint GroupId,
    string BufferName)
{
    public bool AcceptsInput => Type is QuasselBufferType.Channel or QuasselBufferType.Query;
    public bool IsStatusLike => Type is QuasselBufferType.Status or QuasselBufferType.Group;
}
