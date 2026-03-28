namespace Quassel.Client.Domain;

public sealed record ConnectionProfile(
    string Host,
    int Port,
    string Username,
    string Password,
    bool TrustInvalidCertificates = false);
