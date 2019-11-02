//------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Defines the <see cref="SerializedAttribute" />.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    [Conditional("CodeGeneration")]
    public class SerializedAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SerializedAttribute"/> class.
        /// </summary>
        /// <param name="value">The value<see cref="bool"/>.</param>
        public SerializedAttribute(bool value = true)
        {
            Value = value;
        }

        /// <summary>
        /// Gets a value indicating whether Value.
        /// </summary>
        public bool Value { get; }
    }

}
