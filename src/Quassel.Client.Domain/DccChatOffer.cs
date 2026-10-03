using System.Net;

namespace Quassel.Client.Domain;

public sealed record DccChatOffer(string Nick, IPAddress Address, int Port)
{
    public string Endpoint => new IPEndPoint(Address, Port).ToString();
}
