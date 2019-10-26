namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IGuidType
    {
        System.Guid? NullableValue { get; }
        System.Guid Value { get; }
        System.Guid[]? NullableValueArray { get; }
        System.Guid[] ValueArray { get; }
    }
}
