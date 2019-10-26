namespace Chorus.Azure.Cosmos
{
    [CodeGeneration.Chorus.Serialized(false)]
    public interface IResource
    {
        [System.Text.Json.Serialization.JsonPropertyName("_rid")]
        string? ResourceId { get; }

        [System.Text.Json.Serialization.JsonPropertyName("_self")]
        string? SelfLink { get; }

        [System.Text.Json.Serialization.JsonPropertyName("_ts")]
        ulong? Timestamp { get; }

        [System.Text.Json.Serialization.JsonPropertyName("_etag")]
        string? ETag { get; }
    }
}
