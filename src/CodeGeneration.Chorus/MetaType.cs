namespace Chorus.CodeGenerator
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    [DebuggerDisplay("{TypeSymbol?.Name}")]
    internal struct MetaType
    {
        public MetaType(CodeGen codeGen, INamedTypeSymbol typeSymbol)
        {
            Generator = codeGen;
            TypeSymbol = typeSymbol;
        }

        public CodeGen Generator { get; }

        public INamedTypeSymbol TypeSymbol { get; private set; }

        public NameSyntax TypeSyntax
        {
            get { return TypeSymbol.GetFullyQualifiedSymbolName(); }
        }

        public INamespaceSymbol ContainingNamespace
        {
            get { return TypeSymbol.ContainingNamespace; }
        }

        public bool IsDefault
        {
            get { return TypeSymbol == null; }
        }

        public IEnumerable<MetaProperty> LocalProperties
        {
            get
            {
                var that = this;
                return TypeSymbol?.GetMembers().OfType<IPropertySymbol>()
                    .Where(f => !f.IsPropertyIgnored())
                    .Select(f => new MetaProperty(that, f)) ?? ImmutableArray<MetaProperty>.Empty;
            }
        }

        public IEnumerable<MetaProperty> AllProperties
        {
            get
            {
                foreach (var field in InheritedProperties)
                {
                    yield return field;
                }

                foreach (var field in LocalProperties)
                {
                    yield return field;
                }
            }
        }

        public IEnumerable<MetaProperty> InheritedProperties
        {
            get
            {
                if (TypeSymbol == null)
                {
                    yield break;
                }

                foreach (var field in Ancestor.AllProperties)
                {
                    yield return field;
                }
            }
        }

        public IEnumerable<IGrouping<int, MetaProperty>> AllPropertiesByGeneration
        {
            get
            {
                var that = this;
                var results = from generation in that.DefinedGenerations
                              from field in that.AllProperties
                              where field.Generation <= generation
                              group field by generation into fieldsByGeneration
                              select fieldsByGeneration;
                var observedDefaultGeneration = false;
                foreach (var result in results)
                {
                    observedDefaultGeneration |= result.Key == 0;
                    yield return result;
                }

                if (!observedDefaultGeneration)
                {
                    yield return EmptyMetaPropertyGeneration.Default;
                }
            }
        }

        private class EmptyMetaPropertyGeneration : IGrouping<int, MetaProperty>
        {
            internal static readonly IGrouping<int, MetaProperty> Default = new EmptyMetaPropertyGeneration();

            private EmptyMetaPropertyGeneration() { }

            public int Key { get; }

            public IEnumerator<MetaProperty> GetEnumerator() => Enumerable.Empty<MetaProperty>().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public IEnumerable<int> DefinedGenerations => AllProperties.Select(f => f.Generation).Distinct();


        public MetaType Ancestor
        {
            get
            {
                if (TypeSymbol.AllInterfaces.Any())
                {
                    return new MetaType(Generator, TypeSymbol.AllInterfaces[0]);
                }
                return TypeSymbol?.BaseType?.HasAttribute<GenerateClassAttribute>() ?? false
                    ? new MetaType(Generator, TypeSymbol.BaseType)
                    : TypeSymbol.AllInterfaces.Any()
                        ? new MetaType(Generator, TypeSymbol.AllInterfaces[0])
                        : default;
            }
        }

        public IEnumerable<MetaType> Ancestors
        {
            get
            {
                var ancestor = Ancestor;
                while (!ancestor.IsDefault)
                {
                    yield return ancestor;
                    ancestor = ancestor.Ancestor;
                }
            }
        }

        public (INamedTypeSymbol attribute, string field) AbstractAttributes
        {
            get
            {
                var codeGenAtrribute = TypeSymbol?.GetAttributes().FirstOrDefault(a => a.AttributeClass.IsOrDerivesFrom<GenerateClassAttribute>());
                if (codeGenAtrribute != null)
                {
                    var attribute = codeGenAtrribute.NamedArguments.FirstOrDefault(v => v.Key == nameof(GenerateClassAttribute.AbstractAttributeType)).Value;
                    var field = codeGenAtrribute.NamedArguments.FirstOrDefault(v => v.Key == nameof(GenerateClassAttribute.AbstractField)).Value;
                    return ((INamedTypeSymbol)attribute.Value, (string)field.Value);
                }
                return default;
            }
        }

        public TypedConstant AttributeConstructorValue(INamedTypeSymbol typeSymbol)
        {
            var attribute = TypeSymbol?.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, typeSymbol));
            if (attribute != null)
            {
                return attribute.ConstructorArguments[0];
            }
            return default;
        }

        public IEnumerable<MetaType> ThisTypeAndAncestors
        {
            get
            {
                yield return this;
                foreach (var ancestor in Ancestors)
                {
                    yield return ancestor;
                }
            }
        }

        public bool HasAncestor
        {
            get { return !Ancestor.IsDefault; }
        }

        public IEnumerable<MetaType> SelfAndAncestors
        {
            get
            {
                return new[] { this }.Concat(Ancestors);
            }
        }

        public IEnumerable<MetaType> Descendents
        {
            get
            {
                if (Generator == null)
                {
                    return Enumerable.Empty<MetaType>();
                }

                var that = this;
                return from type in Generator.TypesInInputDocument
                       where !SymbolEqualityComparer.Default.Equals(type, that.TypeSymbol)
                       let metaType = new MetaType(that.Generator, type)
                       where metaType.Ancestors.Any(a => SymbolEqualityComparer.Default.Equals(a.TypeSymbol, that.TypeSymbol))
                       select metaType;
            }
        }

        public MetaProperty RecursiveProperty
        {
            get
            {
                var allowedElementTypes = ThisTypeAndAncestors;
                var matches = LocalProperties.Where(f => f.IsCollection && !f.IsDefinitelyNotRecursive && allowedElementTypes.Any(t => SymbolEqualityComparer.Default.Equals(t.TypeSymbol, f.ElementType))).ToList();
                return matches.Count == 1 ? matches.First() : default;
            }
        }

        public MetaType RecursiveType
        {
            get { return !RecursiveProperty.IsDefault ? FindMetaType((INamedTypeSymbol)RecursiveProperty.ElementType) : default; }
        }

        public MetaType RecursiveTypeFromFamily
        {
            get { return GetTypeFamily().SingleOrDefault(t => t.IsRecursiveType); }
        }

        public bool IsRecursiveType
        {
            get
            {
                var that = this;
                return GetTypeFamily().Any(t => that.Equals(t.RecursiveType));
            }
        }

        public bool IsDerivedFromRecursiveType
        {
            get
            {
                var recursiveType = RecursiveTypeFromFamily;
                return !recursiveType.IsDefault && recursiveType.IsAssignableFrom(TypeSymbol);
            }
        }

        /// <summary>Gets the type that contains the collection of this (or a base) type.</summary>
        public MetaType RecursiveParent
        {
            get
            {
                var that = this;
                var result = GetTypeFamily().SingleOrDefault(t => !t.RecursiveType.IsDefault && t.RecursiveType.IsAssignableFrom(that.TypeSymbol));
                return result;
            }
        }

        public bool IsRecursiveParent
        {
            get { return Equals(RecursiveParent); }
        }

        public bool IsRecursiveParentOrDerivative
        {
            get { return IsRecursiveParent || Ancestors.Any(a => a.IsRecursiveParent); }
        }

        public bool IsRecursive => !RecursiveProperty.IsDefault;

        public MetaType RootAncestorOrThisType
        {
            get
            {
                var current = this;
                while (!current.Ancestor.IsDefault)
                {
                    current = current.Ancestor;
                }

                return current;
            }
        }

        public bool ChildrenAreSorted
        {
            get
            {
                // Not very precise, but it does the job for now.
                return RecursiveParent.RecursiveProperty.Type.Name == nameof(ImmutableSortedSet<int>);
            }
        }

        public bool ChildrenAreOrdered
        {
            get
            {
                // Not very precise, but it does the job for now.
                var namedType = RecursiveParent.RecursiveProperty.Type as INamedTypeSymbol;
                return namedType != null && namedType.AllInterfaces.Any(iface => iface.Name == nameof(IReadOnlyList<int>));
            }
        }

        public IEnumerable<MetaProperty> GetPropertiesBeyond(MetaType ancestor)
        {
            if (SymbolEqualityComparer.Default.Equals(ancestor.TypeSymbol, TypeSymbol))
            {
                return ImmutableList.Create<MetaProperty>();
            }

            return ImmutableList.CreateRange(LocalProperties)
                .InsertRange(0, Ancestor.GetPropertiesBeyond(ancestor));
        }

        public bool IsAssignableFrom(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            return SymbolEqualityComparer.Default.Equals(type, TypeSymbol)
                || IsAssignableFrom(type.BaseType);
        }

        public HashSet<MetaType> GetTypeFamily()
        {
            var set = new HashSet<MetaType>();
            var furthestAncestor = Ancestors.LastOrDefault();
            if (furthestAncestor.IsDefault)
            {
                furthestAncestor = this;
            }

            set.Add(furthestAncestor);
            foreach (var relative in furthestAncestor.Descendents)
            {
                set.Add(relative);
            }

            return set;
        }

        public MetaType GetFirstCommonAncestor(MetaType cousin)
        {
            foreach (var ancestor in SelfAndAncestors)
            {
                if (cousin.SelfAndAncestors.Contains(ancestor))
                {
                    return ancestor;
                }
            }

            throw new ArgumentException("No common ancestor found.");
        }

        public override bool Equals(object obj)
        {
            if (obj is MetaType)
            {
                return Equals((MetaType)obj);
            }

            return false;
        }

        public bool Equals(MetaType other)
        {
            return Generator == other.Generator
                && SymbolEqualityComparer.Default.Equals(TypeSymbol, other.TypeSymbol);
        }

        public override int GetHashCode()
        {
            return TypeSymbol?.GetHashCode() ?? 0;
        }

        private MetaType FindMetaType(INamedTypeSymbol type)
        {
            return new MetaType(Generator, type);
        }
    }


}