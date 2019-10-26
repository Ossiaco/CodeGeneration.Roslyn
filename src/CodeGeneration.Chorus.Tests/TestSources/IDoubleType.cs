namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IDoubleType
    {
        double? NullableValue { get; }
        double Value { get; }
        double[]? NullableValueArray { get; }
        double[] ValueArray { get; }
    }
}
