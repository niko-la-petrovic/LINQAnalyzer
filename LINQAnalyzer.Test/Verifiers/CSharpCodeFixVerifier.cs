using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoseLynn;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LINQAnalyzer.Test
{
    public static partial class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
       where TAnalyzer : DiagnosticAnalyzer, new()
       where TCodeFix : CodeFixProvider, new()
    {
        /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyAnalyzerAsync(string, DiagnosticResult[])"/>
        public static async Task VerifyAnalyzerAsync(
            string source,
            IEnumerable<Type> requiredTypes,
            params DiagnosticResult[] expected)
        {
            var test = new Test(
                metadataReferences: MapToMetadataReferences(requiredTypes))
            {
                TestCode = source,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync(CancellationToken.None);
        }

        private static IEnumerable<PortableExecutableReference> MapToMetadataReferences(IEnumerable<Type> requiredTypes)
        {
            return requiredTypes.Select(t => MetadataReferenceFactory.CreateFromType(t));
        }

        /// <inheritdoc cref="CodeFixVerifier{TAnalyzer, TCodeFix, TTest, TVerifier}.VerifyCodeFixAsync(string, DiagnosticResult[], string)"/>
        public static async Task VerifyCodeFixAsync(
            string source,
            DiagnosticResult[] expected,
            string fixedSource,
            IEnumerable<Type> requiredTypes)
        {
            var test = new Test(metadataReferences: MapToMetadataReferences(requiredTypes)
                )
            {
                TestCode = source,
                FixedCode = fixedSource,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync(CancellationToken.None);
        }
    }
}
