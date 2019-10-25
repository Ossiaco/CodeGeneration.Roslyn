namespace Chorus.CodeGenerator
{
    using System;
    using System.Diagnostics;

    public enum JsonStringEnumFormat
    {
        Default,
        LowerCase,
        CamelCase
    }
    [AttributeUsage(AttributeTargets.Enum)]
    [Conditional("CodeGeneration")]
    public class JsonStringEnumAttribute : Attribute
    {
        public JsonStringEnumFormat Format { get; }
        public JsonStringEnumAttribute(JsonStringEnumFormat format = JsonStringEnumFormat.Default)
        {
            Format = format;
        }
    }

}
