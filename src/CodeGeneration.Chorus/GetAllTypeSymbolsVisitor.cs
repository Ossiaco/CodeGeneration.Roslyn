//------------------------------------------------------------
// Copyright (c) Ossiaco Inc.  All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using System.Collections.Immutable;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using System.Threading;
    using Microsoft.CodeAnalysis.CSharp;

    internal class GetAllTypeSymbolsVisitor : SymbolVisitor
    {
        private ImmutableHashSet<INamedTypeSymbol> _values = ImmutableHashSet<INamedTypeSymbol>.Empty.WithComparer(SymbolEqualityComparer.Default);
        private ReaderWriterLockSlim _entryLock = new();


        private GetAllTypeSymbolsVisitor()
        {
        }

        public static ImmutableHashSet<INamedTypeSymbol> Execute(CSharpCompilation compilation)
        {
            var visitor = new GetAllTypeSymbolsVisitor();
            visitor.Visit(compilation.Assembly.GlobalNamespace);
            return visitor._values;
        }

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            Parallel.ForEach(symbol.GetMembers(), s => s.Accept(this));
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            SafeAddToResults(symbol);
        }


        private void SafeAddToResults(INamedTypeSymbol symbol)
        {
            _entryLock.EnterUpgradeableReadLock();
            try
            {
                if (!_values.Contains(symbol))
                {
                    _entryLock.EnterWriteLock();
                    try
                    {
                        if (!_values.Contains(symbol))
                        {
                            _values = _values.Add(symbol);
                        }
                    }
                    finally
                    {
                        _entryLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _entryLock.ExitUpgradeableReadLock();
            }
        }
    }
}
