//------------------------------------------------------------
// Copyright (c) Ossiaco Inc.  All rights reserved.
//------------------------------------------------------------

namespace CodeGeneration.Chorus
{
    using System.Collections.Immutable;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using Microsoft.CodeAnalysis.CSharp;

    internal class GetAllDescendentsSymbolsVisitor : SymbolVisitor
    {
        private readonly INamedTypeSymbol symbol;
        private ImmutableHashSet<INamedTypeSymbol> _values = ImmutableHashSet<INamedTypeSymbol>.Empty.WithComparer(SymbolEqualityComparer.Default);
        private ReaderWriterLockSlim _entryLock = new ReaderWriterLockSlim();


        private GetAllDescendentsSymbolsVisitor(INamedTypeSymbol symbol)
        {
            this.symbol = symbol;
        }

        public static ImmutableHashSet<INamedTypeSymbol> Execute(CSharpCompilation compilation, INamedTypeSymbol symbol)
        {
            var visitor = new GetAllDescendentsSymbolsVisitor(symbol);
            visitor.Visit(compilation.Assembly.GlobalNamespace);
            return visitor._values;
        }

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            Parallel.ForEach(symbol.GetMembers(), s => s.Accept(this));
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            if (IsAssignableFrom(symbol))
            {
                SafeAddToResults(symbol);
            }
        }

        private bool IsAssignableFrom(INamedTypeSymbol symbol)
        {
            if (symbol != null)
            {
                return SymbolEqualityComparer.Default.Equals(this.symbol, symbol)
                    || IsAssignableFrom(symbol.BaseType)
                    || symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(this.symbol, i));
            }
            return false;
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
