//------------------------------------------------------------
// Copyright (c) Ossiaco Inc. All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using System;

    internal class NullProgress<T> : IProgress<T>
    {
        public void Report(T value)
        {
        }
    }

}
