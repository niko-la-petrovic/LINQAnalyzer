using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = LINQAnalyzer.Test.CSharpCodeFixVerifier<
    LINQAnalyzer.LINQAnalyzerAnalyzer,
    LINQAnalyzer.LINQAnalyzerCodeFixProvider>;

namespace LINQAnalyzer.Test
{
    [TestClass]
    public class LINQAnalyzerUnitTest
    {
        //No diagnostics expected to show up
        [TestMethod]
        public async Task NoDiagnostic()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //No diagnostics expected to show up
        [TestMethod]
        public async Task ReturnTypeNoProperties()
        {
            var test = @"using System;
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // Diagnostic showed for AsQueryable
        [TestMethod]
        public async Task AddSelectQueryToEnd()
        {
            var test = @"using System;
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

            var fixtest = @"using System;
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

            var expected = VerifyCS.Diagnostic(LINQAnalyzerAnalyzer.DiagnosticId).WithLocation("/0/Test0.cs", 23, 13).WithArguments("AsQueryable");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
        }
    }
}
