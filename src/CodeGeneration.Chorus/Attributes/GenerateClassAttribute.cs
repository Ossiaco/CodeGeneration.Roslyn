//------------------------------------------------------------
// Copyright (c) Ossiaco Inc.  All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using CodeGeneration.Roslyn;
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Defines the <see cref="GenerateClassAttribute" />
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    [CodeGenerationAttribute(typeof(CodeGenerator))]
    [Conditional("CodeGeneration")]
    public sealed class GenerateClassAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateClassAttribute"/> class.
        /// </summary>
        public GenerateClassAttribute()
        {
        }

        /// <summary>
        /// Gets or sets the AbstractAttributeType
        /// </summary>
        public Type AbstractAttributeType { get; set; }

        /// <summary>
        /// Gets or sets the AbstractField
        /// </summary>
        public string AbstractField { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether IsAbstract
        /// </summary>
        public bool IsAbstract { get; set; } = false;
    }
}
