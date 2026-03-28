namespace Quassel.Client.Domain;

public interface IQuasselId<TValue>
{
    TValue Value { get; }
    bool IsValid { get; }
}

public readonly record struct BufferId(int Value) : IQuasselId<int>
{
    public bool IsValid => Value > 0;
    public override string ToString() => Value.ToString();
    public static implicit operator int(BufferId value) => value.Value;
    public static implicit operator BufferId(int value) => new(value);
}

public readonly record struct NetworkId(int Value) : IQuasselId<int>
{
    public bool IsValid => Value > 0;
    public override string ToString() => Value.ToString();
    public static implicit operator int(NetworkId value) => value.Value;
    public static implicit operator NetworkId(int value) => new(value);
}

public readonly record struct IdentityId(int Value) : IQuasselId<int>
{
    public bool IsValid => Value > 0;
    public override string ToString() => Value.ToString();
    public static implicit operator int(IdentityId value) => value.Value;
    public static implicit operator IdentityId(int value) => new(value);
}

public readonly record struct MsgId(long Value) : IQuasselId<long>
{
    public bool IsValid => Value > 0;
    public override string ToString() => Value.ToString();
    public static implicit operator long(MsgId value) => value.Value;
    public static implicit operator MsgId(long value) => new(value);
}
