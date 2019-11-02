namespace Chorus.Azure.Cosmos
{
    using System;
    using System.Text.Json.Serialization;

    [CodeGeneration.Chorus.Serialized(false)]
    [CodeGeneration.Chorus.GenerateClass]
    public interface IResource2
    {
        [JsonPropertyName("_rid")]
        [CodeGeneration.Chorus.JsonReadOnly]
        string? ResourceId { get; }

        [JsonPropertyName("_self")]
        [CodeGeneration.Chorus.JsonReadOnly]
        string? SelfLink { get; }

        [JsonPropertyName("_ts")]
        [CodeGeneration.Chorus.JsonReadOnly]
        ulong? Timestamp { get; }

        [JsonPropertyName("_etag")]
        [CodeGeneration.Chorus.JsonReadOnly]
        string? ETag { get; }
    }

    [CodeGeneration.Chorus.GenerateClass(IsAbstract = true, AbstractAttributeType = typeof(Chorus.Common.Messaging.MessageTypeAttribute), AbstractField = nameof(Type))]
    public interface IMessage : IResource2
    {
        /// <summary>
        /// Gets the ActorId.
        /// </summary>
        [JsonPropertyName("_aid")]
        Guid? ActorId { get; }

        /// <summary>
        /// Gets the CorrelationId.
        /// </summary>
        [JsonPropertyName("_cid")]
        Guid? CorrelationId { get; }

        /// <summary>
        /// Gets the Id.
        /// </summary>
        Guid? Id { get; }

        /// <summary>
        /// Gets the Type.
        /// </summary>
        [JsonPropertyName("_t")]
        [CodeGeneration.Chorus.JsonReadOnly]
        uint Type { get; }

        /// <summary>
        /// Gets the UserId.
        /// </summary>
        [JsonPropertyName("_uid")]
        Guid? UserId { get; }
    }


    [Chorus.Common.Messaging.MessageTypeAttribute(0x00000001)]
    public interface ISomeMessage : IMessage
    {
        string SomeProperty { get; }
    }

}
