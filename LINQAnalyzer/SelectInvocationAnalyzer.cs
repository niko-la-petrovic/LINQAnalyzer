using LINQAnalyzer.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace LINQAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SelectInvocationAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = Diagnostics.LA0001;

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.SelectInvocationAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.SelectInvocationAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.SelectInvocationAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "LINQ";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeSyntaxNodeExpression, SyntaxKind.ExpressionStatement);
        }

        protected static List<Action<SyntaxNodeAnalysisContext, ExpressionStatementSyntax>>
           ExpressionStatementAnalyzeActions = new List<Action<SyntaxNodeAnalysisContext, ExpressionStatementSyntax>>()
           {
               (c, x) => AnalyzeQueryableSelectInvocation(c, x),
           };

        protected static void AnalyzeSyntaxNodeExpression(SyntaxNodeAnalysisContext context)
        {
            var expression = (ExpressionStatementSyntax)context.Node;
            foreach (var analysisAction in ExpressionStatementAnalyzeActions)
                analysisAction.Invoke(context, expression);
        }

        protected static void AnalyzeQueryableSelectInvocation(SyntaxNodeAnalysisContext context, ExpressionStatementSyntax expression)
        {
            if (!AnalyzerUtils.LastNodeIsInvocationExpressionSyntax(expression, out var invocationExpressionSyntax))
                return;

            var methodSymbol = context
                .SemanticModel
                .GetSymbolInfo(invocationExpressionSyntax, context.CancellationToken)
                .Symbol as IMethodSymbol;
            if (methodSymbol == null || !methodSymbol.IsGenericMethod || methodSymbol.Name == "Select")
                return;

            var returnTypeSymbol = methodSymbol.ReturnType;
            var genericTypeSymbol = ((INamedTypeSymbol)returnTypeSymbol).TypeArguments.First();
            bool genericTypeHasProperties = genericTypeSymbol
                .GetMembers().Any(m => m.Kind == SymbolKind.Property);
            if (!genericTypeHasProperties)
                return;

            var compilation = context.Compilation;
            var iQueryableType = compilation.GetTypeByMetadataName("System.Linq.IQueryable`1");

            var areEqual = SymbolEqualityComparer.Default.Equals(iQueryableType, returnTypeSymbol.OriginalDefinition);
            if (!areEqual)
                return;

            var diagnostic = Diagnostic.Create(Rule, invocationExpressionSyntax.GetLocation(), methodSymbol.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }
}
