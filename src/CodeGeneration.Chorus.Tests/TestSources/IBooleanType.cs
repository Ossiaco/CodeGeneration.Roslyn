namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IBooleanType
    {
        bool? NullableValue { get; }
        bool Value { get; }
        bool[]? NullableValueArray { get; }
        bool[] ValueArray { get; }
    }
}
