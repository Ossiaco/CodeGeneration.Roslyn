namespace Chorus.CodeGenerator
{
    using System.Collections.Generic;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal abstract class FeatureGenerator
    {
        protected readonly CodeGen _generator;
        protected readonly List<MemberDeclarationSyntax> _innerMembers = new List<MemberDeclarationSyntax>();
        protected readonly List<MemberDeclarationSyntax> _siblingMembers = new List<MemberDeclarationSyntax>();
        protected readonly List<BaseTypeSyntax> _baseTypes = new List<BaseTypeSyntax>();
        protected readonly List<StatementSyntax> _additionalCtorStatements = new List<StatementSyntax>();
        protected readonly MetaType _applyTo;

        protected FeatureGenerator(CodeGen generator)
        {
            _generator = generator;
            _applyTo = generator.InterfaceMetaType;
        }

        public abstract bool IsApplicable { get; }

        protected virtual BaseTypeSyntax[] AdditionalApplyToBaseTypes
        {
            get { return _baseTypes.ToArray(); }
        }

        public void Generate()
        {
            GenerateCore();
        }

        public virtual ClassDeclarationSyntax ProcessApplyToClassDeclaration(ClassDeclarationSyntax applyTo)
        {
            var additionalApplyToBaseTypes = AdditionalApplyToBaseTypes;
            if (additionalApplyToBaseTypes != null && additionalApplyToBaseTypes.Length > 0)
            {
                applyTo = applyTo.WithBaseList(
                    (applyTo.BaseList ?? SyntaxFactory.BaseList()).AddTypes(additionalApplyToBaseTypes));
            }

            if (_innerMembers.Count > 0)
            {
                applyTo = applyTo.AddMembers(_innerMembers.ToArray());
            }

            if (_additionalCtorStatements.Count > 0)
            {
                var origCtor = applyTo.GetMeaningfulConstructor();
                var updatedCtor = origCtor.AddBodyStatements(_additionalCtorStatements.ToArray());
                applyTo = applyTo.ReplaceNode(origCtor, updatedCtor);
            }

            return applyTo;
        }

        public virtual SyntaxList<MemberDeclarationSyntax> ProcessFinalGeneratedResult(SyntaxList<MemberDeclarationSyntax> applyToAndOtherTypes)
        {
            if (_siblingMembers.Count > 0)
            {
                applyToAndOtherTypes = applyToAndOtherTypes.AddRange(_siblingMembers);
            }

            return applyToAndOtherTypes;
        }

        protected abstract void GenerateCore();
    }


}