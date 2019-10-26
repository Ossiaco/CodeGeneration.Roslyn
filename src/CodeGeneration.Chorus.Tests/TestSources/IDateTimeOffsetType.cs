namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IDateTimeOffsetType
    {
        System.DateTimeOffset? NullableValue { get; }
        System.DateTimeOffset Value { get; }
        System.DateTimeOffset[]? NullableValueArray { get; }
        System.DateTimeOffset[] ValueArray { get; }
    }

}
