namespace Chorus.CodeGenerator
{
    using System;
    using System.Diagnostics;
    using CodeGeneration.Roslyn;

    [AttributeUsage(AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    [CodeGenerationAttribute(typeof(CodeGenerator))]
    [Conditional("CodeGeneration")]
    public class GenerateClassAttribute : Attribute
    {
        public GenerateClassAttribute()
        {
        }
        public bool IsAbstract { get; set; } = false;
        
        public Type AbstractAttributeType { get; set; }

        public string AbstractField { get; set; }
    }
}