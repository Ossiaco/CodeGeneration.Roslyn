//------------------------------------------------------------
// Copyright (c) Ossiaco Inc.  All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using System.Text.Json;

    /// <summary>
    /// Defines the <see cref="IJsonSerializeable" />.
    /// </summary>
    public interface IJsonSerializeable
    {
        /// <summary>
        /// The ToJson
        /// </summary>
        /// <param name="writer">The writer<see cref="Utf8JsonWriter"/>.</param>
        void ToJson(Utf8JsonWriter writer);
    }
}
