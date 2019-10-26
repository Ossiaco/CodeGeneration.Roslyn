namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IDecimalType
    {
        decimal? NullableValue { get; }
        decimal Value { get; }
        decimal[]? NullableValueArray { get; }
        decimal[] ValueArray { get; }
    }
}
