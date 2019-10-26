namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IUint16Type
    {
        ushort? NullableValue { get; }
        ushort Value { get; }
        ushort[]? NullableValueArray { get; }
        ushort[] ValueArray { get; }
    }
}
