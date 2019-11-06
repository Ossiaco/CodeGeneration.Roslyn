﻿//------------------------------------------------------------
// Copyright (c) Ossiaco Inc.  All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Defines the JsonStringEnumFormat.
    /// </summary>
    public enum JsonStringEnumFormat
    {
        /// <summary>
        /// Defines the Default
        /// </summary>
        Default,

        /// <summary>
        /// Defines the LowerCase
        /// </summary>
        LowerCase,

        /// <summary>
        /// Defines the CamelCase.
        /// </summary>
        CamelCase,
    }

    /// <summary>
    /// Defines the <see cref="JsonStringEnumAttribute" />.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum)]
    [Conditional("CodeGeneration")]
    public class JsonStringEnumAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonStringEnumAttribute"/> class.
        /// </summary>
        /// <param name="format">The format<see cref="JsonStringEnumFormat"/>.</param>
        public JsonStringEnumAttribute(JsonStringEnumFormat format = JsonStringEnumFormat.Default)
        {
            Format = format;
        }

        /// <summary>
        /// Gets the Format.
        /// </summary>
        public JsonStringEnumFormat Format { get; }
    }

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
