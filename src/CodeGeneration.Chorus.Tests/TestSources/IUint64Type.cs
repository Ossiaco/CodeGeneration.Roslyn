namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IUint64Type
    {
        ulong? NullableValue { get; }
        ulong Value { get; }
        ulong[]? NullableValueArray { get; }
        ulong[] ValueArray { get; }
    }
}
