using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VerifyCS = LINQAnalyzer.Test.CSharpCodeFixVerifier<
    LINQAnalyzer.SelectInvocationAnalyzer,
    LINQAnalyzer.SelectInvocationCodeFixProvider>;

namespace LINQAnalyzer.Test
{
    [TestClass]
    public class SelectInvocationTests
    {
        //No diagnostics expected to show up
        [TestMethod]
        public async Task NoDiagnostic()
        {
            var testInput = @"";

            await VerifyCS.VerifyAnalyzerAsync(testInput);
        }

        //No diagnostics expected to show up
        [TestMethod]
        public async Task ReturnTypeNoProperties()
        {
            var testInput = @"using System;
using System.Collections.Generic;
using System.Linq;

namespace Analyzer1Test
{

    class TestClass
    {
        string Id = ""id"";
        string Name = ""name"";
    }

    class Program
    {
        static void Main(string[] args)
        {

            List<TestClass> l = new List<TestClass> {
                new TestClass(){},
                new TestClass(){}};

            l.AsQueryable();

        }
}
}";

            await VerifyCS.VerifyAnalyzerAsync(testInput);
        }

        // Diagnostic showed for AsQueryable
        [TestMethod]
        public async Task AddSelectQueryToEnd_Diagnostic()
        {
            var testInput = @"using System;
using System.Collections.Generic;
using System.Linq;

namespace Analyzer1Test
{

    class TestClass
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {

            List<TestClass> l = new List<TestClass> {
                new TestClass { Id = ""1"", Name = ""Name1"" },
                new TestClass { Id = ""2"", Name = ""Name2"" } };

            l.AsQueryable();

        }
}
}";

            var diagnostic = VerifyCS.Diagnostic(SelectInvocationAnalyzer.DiagnosticId).WithLocation("/0/Test0.cs", 23, 13).WithArguments("AsQueryable");
            await VerifyCS.VerifyAnalyzerAsync(testInput, diagnostic);
        }

        // Diagnostic showed for AsQueryable
        [TestMethod]
        public async Task AddSelectQueryToEnd_CodeFix()
        {
            var testInput = @"using System;
using System.Collections.Generic;
using System.Linq;

namespace Analyzer1Test
{

    class TestClass
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {

            List<TestClass> l = new List<TestClass> {
                new TestClass { Id = ""1"", Name = ""Name1"" },
                new TestClass { Id = ""2"", Name = ""Name2"" } };

            l.AsQueryable();

        }
}
}";

            var expected = @"using System;
using System.Collections.Generic;
using System.Linq;

namespace Analyzer1Test
{

    class TestClass
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {

            List<TestClass> l = new List<TestClass> {
                new TestClass { Id = ""1"", Name = ""Name1"" },
                new TestClass { Id = ""2"", Name = ""Name2"" } };

            l.AsQueryable().Select(t => new TestClass { Id = t.Id, Name = t.Name });

        }
}
}";

            var diagnostic = VerifyCS.Diagnostic(SelectInvocationAnalyzer.DiagnosticId).WithLocation("/0/Test0.cs", 23, 13).WithArguments("AsQueryable");
            await VerifyCS.VerifyCodeFixAsync(testInput, diagnostic, expected);
        }

        internal class TestClass
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        protected void ManualTest()
        {
            List<TestClass> l = new List<TestClass> {
                new TestClass { Id = "1", Name = "Name1" },
                new TestClass { Id = "2", Name = "Name2" } };

            l.AsQueryable();
        }
    }
}
