namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface ISingleType
    {
        float? NullableValue { get; }
        float Value { get; }
        float[]? NullableValueArray { get; }
        float[] ValueArray { get; }
    }
}
