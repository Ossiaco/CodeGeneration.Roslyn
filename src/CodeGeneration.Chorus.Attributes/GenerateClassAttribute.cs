//------------------------------------------------------------
// Copyright (c) Ossiaco Inc.  All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Defines the <see cref="GenerateClassAttribute" />.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    // [Conditional("CodeGeneration")]
    public sealed class GenerateClassAttribute : CodeGenerationAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateClassAttribute"/> class.
        /// </summary>
        public GenerateClassAttribute()
        {
        }

        /// <summary>
        /// Gets or sets the AbstractAttributeType.
        /// </summary>
        public Type AbstractAttributeType { get; set; }

        /// <summary>
        /// Gets or sets the AbstractField
        /// </summary>
        public string AbstractField { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsAbstract.
        /// </summary>
        public bool IsAbstract { get; set; } = false;
    }
}
