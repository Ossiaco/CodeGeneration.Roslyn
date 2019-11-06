//------------------------------------------------------------
// Copyright (c) Ossiaco Inc.  All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Defines the <see cref="JsonPropertyNameAttributeExtensions" />
    /// </summary>
    public static class JsonPropertyNameAttributeExtensions
    {
        /// <summary>
        /// TypeScriptPropertyName.
        /// </summary>
        /// <param name="attribute">The attribute<see cref="JsonPropertyNameAttribute"/>.</param>
        /// <returns>A <see cref="string"/>.</returns>
        public static string TypeScriptPropertyName(this JsonPropertyNameAttribute attribute) => attribute.Name.Contains("-") ? $"\"{attribute.Name}\"" : attribute.Name;
    }
}
