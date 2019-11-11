//------------------------------------------------------------
// Copyright (c) Ossiaco Inc.  All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Defines the <see cref="JsonReadOnlyAttribute" />.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    // [Conditional("CodeGeneration")]
    public class JsonReadOnlyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonReadOnlyAttribute"/> class.
        /// </summary>
        public JsonReadOnlyAttribute()
        {
        }
    }
}
