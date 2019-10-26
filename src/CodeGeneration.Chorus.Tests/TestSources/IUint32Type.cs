namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IUint32Type
    {
        uint? NullableValue { get; }
        uint Value { get; }
        uint[]? NullableValueArray { get; }
        uint[] ValueArray { get; }
    }
}
