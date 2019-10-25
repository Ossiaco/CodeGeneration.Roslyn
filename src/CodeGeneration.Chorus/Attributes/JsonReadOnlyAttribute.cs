namespace Chorus.CodeGenerator
{
    using System;
    using System.Diagnostics;

    [AttributeUsage(AttributeTargets.Property)]
    [Conditional("CodeGeneration")]
    public class JsonReadOnlyAttribute : Attribute
    {
        public JsonReadOnlyAttribute()
        {
        }
    }

}
