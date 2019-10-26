namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IInt32Type
    {
        int? NullableValue { get; }
        int Value { get; }
        int[]? NullableValueArray { get; }
        int[] ValueArray { get; }
    }
}
