using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace LINQAnalyzer.Util
{
    public static class AnalyzerUtils
    {
        public static bool LastNodeIsInvocationExpressionSyntax(SyntaxNode expression,
            out InvocationExpressionSyntax invocationExpressionSyntax)
        {
            invocationExpressionSyntax = expression.ChildNodes().LastOrDefault() as InvocationExpressionSyntax;
            return invocationExpressionSyntax != null;
        }
    }
}
