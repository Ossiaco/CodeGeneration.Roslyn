namespace Chorus.CodeGenerator
{
    using System;
    using System.Collections.Immutable;
    using Microsoft.CodeAnalysis;

    public class CodeGenOptions
    {
        private readonly ImmutableDictionary<string, TypedConstant> _data;
        public CodeGenOptions()
        {
        }

        public CodeGenOptions(AttributeData attributeData)
        {
            AttributeData = attributeData;
            _data = attributeData.NamedArguments.ToImmutableDictionary(kv => kv.Key, kv => kv.Value);
        }

        public AttributeData AttributeData { get; }

        public bool IsAbstract => (bool)(_data.GetValueOrDefault(nameof(GenerateClassAttribute.IsAbstract)).Value ?? false);
        public INamedTypeSymbol AbstractAttributeType => (INamedTypeSymbol)(_data.GetValueOrDefault(nameof(GenerateClassAttribute.AbstractField)).Value);
        public string AbstractField => (string)(_data.GetValueOrDefault(nameof(GenerateClassAttribute.AbstractField)).Value);
    }

}