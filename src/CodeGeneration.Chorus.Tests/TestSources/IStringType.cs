namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IStringType
    {
        string? NullableValue { get; }
        string Value { get; }
        string[]? NullableValueArray { get; }
        string[] ValueArray { get; }
    }
}
