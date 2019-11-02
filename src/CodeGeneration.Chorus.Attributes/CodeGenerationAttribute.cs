//------------------------------------------------------------
// Copyright (c) Ossiaco Inc.  All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Defines the <see cref="CodeGenerationAttribute" />.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    [Conditional("CodeGeneration")]
    public abstract class CodeGenerationAttribute : Attribute
    {
    }
}
