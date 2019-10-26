namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IByteType
    {
        byte? NullableValue { get; }
        byte Value { get; }
        byte[]? NullableValueArray { get; }
        byte[] ValueArray { get; }
    }
#nullable disable
}
