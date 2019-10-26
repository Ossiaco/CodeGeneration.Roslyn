namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.Serialized(false)]
    [CodeGeneration.Chorus.GenerateClass()]
    public interface IResource2
    {
        [System.Text.Json.Serialization.JsonPropertyName("_rid")]
        [CodeGeneration.Chorus.JsonReadOnly]
        string? ResourceId { get; }

        [System.Text.Json.Serialization.JsonPropertyName("_self")]
        [CodeGeneration.Chorus.JsonReadOnly]
        string? SelfLink { get; }

        [System.Text.Json.Serialization.JsonPropertyName("_ts")]
        [CodeGeneration.Chorus.JsonReadOnly]
        ulong? Timestamp { get; }

        [System.Text.Json.Serialization.JsonPropertyName("_etag")]
        [CodeGeneration.Chorus.JsonReadOnly]
        string? ETag { get; }
    }
}
