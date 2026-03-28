namespace Quassel.Client.Domain;

[Flags]
public enum QuasselMessageType
{
    Plain = 0x00001,
    Notice = 0x00002,
    Action = 0x00004,
    Nick = 0x00008,
    Mode = 0x00010,
    Join = 0x00020,
    Part = 0x00040,
    Quit = 0x00080,
    Kick = 0x00100,
    Kill = 0x00200,
    Server = 0x00400,
    Info = 0x00800,
    Error = 0x01000,
    DayChange = 0x02000,
    Topic = 0x04000,
    NetsplitJoin = 0x08000,
    NetsplitQuit = 0x10000,
    Invite = 0x20000,
}

[Flags]
public enum QuasselMessageFlags
{
    None = 0x00,
    Self = 0x01,
    Highlight = 0x02,
    Redirected = 0x04,
    ServerMessage = 0x08,
    StatusMessage = 0x10,
    Ignored = 0x20,
    Backlog = 0x80,
}

public enum QuasselBufferType
{
    Invalid = 0x00,
    Status = 0x01,
    Channel = 0x02,
    Query = 0x04,
    Group = 0x08,
}

public enum QuasselConnectionState
{
    Disconnected,
    Connecting,
    Negotiating,
    Encrypting,
    Registering,
    Authenticating,
    Synchronizing,
    Ready,
    Error,
}
