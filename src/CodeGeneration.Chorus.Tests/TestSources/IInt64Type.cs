namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IInt64Type
    {
        long? NullableValue { get; }
        long Value { get; }
        long[]? NullableValueArray { get; }
        long[] ValueArray { get; }
    }
}
