namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IInt16Type
    {
        short? NullableValue { get; }
        short Value { get; }
        short[]? NullableValueArray { get; }
        short[] ValueArray { get; }
    }
}
