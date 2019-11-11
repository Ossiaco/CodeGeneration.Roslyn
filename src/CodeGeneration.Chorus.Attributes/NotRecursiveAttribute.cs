//------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Defines the <see cref="NotRecursiveAttribute" />.
    /// </summary>
    // [Conditional("CodeGeneration")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class NotRecursiveAttribute : Attribute
    {
    }
}
