// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MS-PL license. See LICENSE.txt file in the project root for full license information.

using System.Threading.Tasks;
using Xunit;

public class DocumentTransformTests : CompilationTestsBase
{
    [Fact]
    public async Task EmptyFile_NoGeneratorsAsync()
    {
        await AssertGeneratedAsExpectedAsync("", "");
    }

    [Fact]
    public async Task Usings_WhenNoCode_CopiedToOutputAsync()
    {
        const string usings = "using System;";
        await AssertGeneratedAsExpectedAsync(usings, usings);
    }

    [Fact]
    public async Task AncestorTree_IsBuiltProperlyAsyncAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

[EmptyPartial]
partial class Empty {}

namespace Testing.Middle
{
    using System.Linq;

    namespace Inner
    {
        partial class OuterClass<T>
        {
            partial struct InnerStruct<T1, T2>
            {
                [EmptyPartial]
                int Placeholder { get; }
            }
        }
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

partial class Empty
{
}

namespace Testing.Middle
{
    using System.Linq;

    namespace Inner
    {
        partial class OuterClass<T>
        {
            partial struct InnerStruct<T1, T2>
            {
            }
        }
    }
}";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task DefineDirective_DroppedAsync()
    {
        // define directives must be leading any other tokens to be valid in C#
        const string source = @"
#define SOMETHING
using System;
using System.Linq;";
        const string generated = @"
using System;
using System.Linq;";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task Comment_BetweenUsings_DroppedAsync()
    {
        const string source = @"
using System;
// one line comment
using System.Linq;";
        const string generated = @"
using System;
using System.Linq;";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task Region_TrailingUsings_DroppedAsync()
    {
        const string source = @"
using System;
#region CustomRegion
using System.Linq;
#endregion //CustomRegion";
        const string generated = @"
using System;
using System.Linq;";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task IAsyncfElseDirective_OnUsings_InactiveUsingAndDirectives_DroppedAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;
#if SOMETHING_ACTIVE
using System.Linq;
#elif SOMETHING_INACTIVE
using System.Diagnostics;
#else
using System.Never;
#endif

[EmptyPartial]
partial class Empty {}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;
using System.Linq;

partial class Empty
{
}";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task RegionDirective_InsideClass_DroppedAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

partial class Empty
{
#region SomeRegion
    [EmptyPartial]
    int Counter { get; }
#endregion
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

partial class Empty
{
}";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task RegionDirective_InsideStruct_DroppedAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

partial struct Empty
{
#region SomeRegion
    [EmptyPartial]
    int Counter { get; }
#endregion
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

partial struct Empty
{
}";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task RegionDirective_InsideNamespace_DroppedAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
#region SomeRegion
    [EmptyPartial]
    partial class Empty { }
#endregion
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    partial class Empty
    {
    }
}";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task Class_Modifiers_ArePreserved_WithoutTriviaAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    // some one-line comment
    public static partial class Empty
    {
        [EmptyPartial]
        public static int Method() => 0;
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    public static partial class Empty
    {
    }
}";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task Struct_Modifiers_ArePreserved_WithoutTriviaAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    // some one-line comment
    internal partial struct Empty
    {
        [EmptyPartial]
        public static int Method() => 0;
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    internal partial struct Empty
    {
    }
}";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task Class_TypeParameters_ArePreservedAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    partial class Empty<T> where T : class
    {
        [EmptyPartial]
        public static T Method() => null;
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    partial class Empty<T>
    {
    }
}";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task Struct_TypeParameters_ArePreservedAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    partial struct Empty<T> where T : class
    {
        [EmptyPartial]
        public static T Method() => null;
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    partial struct Empty<T>
    {
    }
}";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task RichGenerator_Wraps_InOtherNamespaceAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    [DuplicateInOtherNamespace(""Other.Namespace"")]
    class Something
    {
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Other.Namespace
{
    class Something
    {
    }
}";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task RichGenerator_Adds_UsingAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    [AddGeneratedUsing(""System.Collections.Generic"")]
    partial class Something
    {
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;
using System.Collections.Generic;

";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task RichGenerator_Adds_ExternAliasAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    [AddGeneratedExtern(""MyExternAlias"")]
    partial class Something
    {
    }
}";
        const string generated = @"
extern alias MyExternAlias;

using System;
using CodeGeneration.Roslyn.Tests.Generators;

";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task RichGenerator_Adds_AttributeAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    [AddGeneratedAttribute(""GeneratedAttribute"")]
    partial class Something
    {
    }
}";
        const string generated = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

[GeneratedAttribute]
";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }

    [Fact]
    public async Task RichGenerator_Appends_MultipleResultsAsync()
    {
        const string source = @"
using System;
using CodeGeneration.Roslyn.Tests.Generators;

namespace Testing
{
    [DuplicateInOtherNamespace(""Other.Namespace1"")]
    [DuplicateInOtherNamespace(""Other.Namespace2"")]
    [AddGeneratedUsing(""System.Collections"")]
    [AddGeneratedUsing(""System.Collections.Generic"")]
    [AddGeneratedExtern(""MyExternAlias1"")]
    [AddGeneratedExtern(""MyExternAlias2"")]
    [AddGeneratedAttribute(""GeneratedAttribute"")]
    [AddGeneratedAttribute(""GeneratedAttribute"")]
    partial class Something
    {
    }
}";
        const string generated = @"
extern alias MyExternAlias1;
extern alias MyExternAlias2;

using System;
using CodeGeneration.Roslyn.Tests.Generators;
using System.Collections;
using System.Collections.Generic;

[GeneratedAttribute]
[GeneratedAttribute]
namespace Other.Namespace1
{
    class Something
    {
    }
}

namespace Other.Namespace2
{
    class Something
    {
    }
}
";
        await AssertGeneratedAsExpectedAsync(source, generated);
    }
}
