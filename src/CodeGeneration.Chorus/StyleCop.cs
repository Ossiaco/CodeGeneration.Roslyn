namespace Chorus.CodeGenerator
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using System;
    using System.Linq;
    using System.Reflection;

    internal static class StyleCop
    {
        internal static int Sort(MemberDeclarationSyntax first, MemberDeclarationSyntax second)
        {
            // Requires.NotNull(first, "first");
            // Requires.NotNull(second, "second");

            var firstOrder = GetMemberDeclarationTypeOrder(first);
            var secondOrder = GetMemberDeclarationTypeOrder(second);

            var compareResult = firstOrder.CompareTo(secondOrder);
            if (compareResult == 0)
            {
                firstOrder = GetMemberDeclarationVisibilityOrder(first);
                secondOrder = GetMemberDeclarationVisibilityOrder(second);
                compareResult = firstOrder.CompareTo(secondOrder);

                if (compareResult == 0)
                {
                    var firstIsStatic = first.HasModifier(SyntaxKind.StaticKeyword);
                    var secondIsStatic = second.HasModifier(SyntaxKind.StaticKeyword);
                    if (firstIsStatic && !secondIsStatic)
                    {
                        compareResult = -1;
                    }
                    else if (!firstIsStatic && secondIsStatic)
                    {
                        compareResult = 1;
                    }

                    if (compareResult == 0)
                    {
                        var firstIsReadOnly = first.HasModifier(SyntaxKind.ReadOnlyKeyword);
                        var secondIsReadOnly = second.HasModifier(SyntaxKind.ReadOnlyKeyword);
                        if (firstIsReadOnly && !secondIsReadOnly)
                        {
                            compareResult = -1;
                        }
                        else if (!firstIsReadOnly && secondIsReadOnly)
                        {
                            compareResult = 1;
                        }

                        if (compareResult == 0)
                        {
                            var firstName = GetName(first);
                            var secondName = GetName(second);
                            if (firstName.HasValue && secondName.HasValue)
                            {
                                compareResult = string.Compare(firstName.Value.ValueText, secondName.Value.ValueText, StringComparison.CurrentCulture);
                            }
                        }
                    }
                }
            }

            return compareResult;
        }

        private static int GetMemberDeclarationTypeOrder(MemberDeclarationSyntax member)
        {
            for (var i = 0; i < MemberDeclarationOrder.Length; i++)
            {
                if (MemberDeclarationOrder[i].GetTypeInfo().IsInstanceOfType(member))
                {
                    return i;
                }
            }

            return -1;
        }

        private static SyntaxTokenList? GetModifiers(MemberDeclarationSyntax member)
        {
            var modifiers =
                (member as BaseMethodDeclarationSyntax)?.Modifiers ??
                (member as BasePropertyDeclarationSyntax)?.Modifiers ??
                (member as BaseFieldDeclarationSyntax)?.Modifiers ??
                (member as BaseTypeDeclarationSyntax)?.Modifiers;

            return modifiers;
        }

        private static SyntaxToken? GetName(MemberDeclarationSyntax member)
        {
            var name =
                (member as MethodDeclarationSyntax)?.Identifier ??
                (member as PropertyDeclarationSyntax)?.Identifier ??
                (member as FieldDeclarationSyntax)?.Declaration.Variables.FirstOrDefault()?.Identifier ??
                (member as TypeDeclarationSyntax)?.Identifier;

            return name;
        }

        private static bool HasModifier(this MemberDeclarationSyntax member, SyntaxKind modifier)
        {
            return GetModifiers(member)?.Any(t => t.IsKind(modifier)) ?? false;
        }

        private static int GetMemberDeclarationVisibilityOrder(MemberDeclarationSyntax member)
        {
            var modifiers = GetModifiers(member);

            if (modifiers.HasValue)
            {
                var isPublic = member.HasModifier(SyntaxKind.PublicKeyword);
                var isProtected = member.HasModifier(SyntaxKind.ProtectedKeyword);
                var isInternal = member.HasModifier(SyntaxKind.InternalKeyword);
                var isPrivate = member.HasModifier(SyntaxKind.PrivateKeyword);
                if (isPublic)
                {
                    return 0;
                }
                else if (isProtected && isInternal)
                {
                    return 1;
                }
                else if (isInternal)
                {
                    return 2;
                }
                else if (isProtected)
                {
                    return 3;
                }
                else if (isPrivate)
                {
                    return 4;
                }
                else // assume private-level
                {
                    return 4;
                }
            }

            return -1;
        }

        private static readonly Type[] MemberDeclarationOrder = new Type[] {
            typeof(FieldDeclarationSyntax),
            typeof(ConstructorDeclarationSyntax),
            typeof(ConversionOperatorDeclarationSyntax),
            typeof(OperatorDeclarationSyntax),
            typeof(EnumDeclarationSyntax),
            typeof(PropertyDeclarationSyntax),
            typeof(MethodDeclarationSyntax),
            typeof(ClassDeclarationSyntax),
            typeof(StructDeclarationSyntax),
        };
    }
}
