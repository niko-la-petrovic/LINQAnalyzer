using Microsoft.CodeAnalysis.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using VerifyCS = LINQAnalyzer.Test.CSharpCodeFixVerifier<
    LINQAnalyzer.ICollectionAnalyzer,
    LINQAnalyzer.ICollectionCodeFixProvider>;

namespace LINQAnalyzer.Test
{
    [TestClass]
    public class ICollectionPropertyTests
    {
        //No diagnostics expected to show up
        [TestMethod]
        public async Task NoDiagnostic()
        {
            var testInput = @"";

            await VerifyCS.VerifyAnalyzerAsync(testInput);
        }

        [TestMethod]
        public async Task RewriteEntityProperty_Diagnostic()
        {
            var testInput = @"using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analyzer1Test
{
    internal enum TestEnum
    {
        A,
        B,
        C
    }

    internal class TestClass
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public virtual ICollection<TestEnum> TestEnums { get; set; }
    }

    internal class TestClassConfiguration : IEntityTypeConfiguration<TestClass>
    {
        public void Configure(EntityTypeBuilder<TestClass> builder)
        {
            builder
                .Property(t => t.TestEnums);
        }
    }
}";

            var requiredTypes = new List<Type> { typeof(IEntityTypeConfiguration<>),
                typeof(EntityTypeBuilder<>)};

            var diagnostic = VerifyCS.Diagnostic(ICollectionAnalyzer.DiagnosticId).WithLocation("/0/Test0.cs", 26, 13).WithArguments("Property");
            await VerifyCS.VerifyAnalyzerAsync(testInput, requiredTypes, diagnostic);
        }

        [TestMethod]
        public async Task RewriteEntityProperty_CodeFix()
        {
            var testInput = @"using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analyzer1Test
{
    public enum TestEnum
    {
        A,
        B,
        C
    }

    public class TestClass
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public virtual ICollection<TestEnum> TestEnums { get; set; }
    }

    public class TestClassConfiguration : IEntityTypeConfiguration<TestClass>
    {
        public void Configure(EntityTypeBuilder<TestClass> builder)
        {
            builder
                .Property(t => t.TestEnums);
        }
    }
}";

            var testOutput = @"using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Analyzer1Test
{
    public enum TestEnum
    {
        A,
        B,
        C
    }

    public class TestClass : IEnumsTest
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public virtual ICollection<TestEnum> TestEnums { get; set; }

        public bool IsA { get; set; }

        public bool IsB { get; set; }

        public bool IsC { get; set; }
    }

    public class TestClassConfiguration : IEntityTypeConfiguration<TestClass>
    {
        public void Configure(EntityTypeBuilder<TestClass> builder)
        {
        }
    }

    public interface IEnumsTest
    {
        public bool IsA { get; }

        public bool IsB { get; }

        public bool IsC { get; }

        bool IsTestToEnums(TestEnum testEnum)
        {
            return testEnum switch
            {
            TestEnum.A => IsA, TestEnum.B => IsB, TestEnum.C => IsC, _ => false, }

            ;
        }

        ICollection<TestEnum> TestEnums { get; }

        void AddTestEnums(TestEnum testEnum)
        {
            TestEnums.Add(testEnum);
        }
    }
}";

            var requiredTypes = new List<Type> { typeof(IEntityTypeConfiguration<>),
                typeof(EntityTypeBuilder<>)};

            var diagnostic = VerifyCS.Diagnostic(ICollectionAnalyzer.DiagnosticId).WithLocation("/0/Test0.cs", 26, 13).WithArguments("Property");
            await VerifyCS.VerifyAnalyzerAsync(testInput, requiredTypes, diagnostic);
            await VerifyCS.VerifyCodeFixAsync(testInput, new DiagnosticResult[] { diagnostic }, testOutput, requiredTypes);
        }

        internal enum TestEnum
        {
            A,
            B,
            C
        }

        internal class TestClass
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public virtual ICollection<TestEnum> TestEnums { get; set; }
        }

        internal class TestClassConfiguration : IEntityTypeConfiguration<TestClass>
        {
            public void Configure(EntityTypeBuilder<TestClass> builder)
            {
                builder.Property(t => t.TestEnums);
            }
        }
    }
}
