using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace LINQAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class LINQAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "LINQAnalyzer";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "LINQ";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            //context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeSyntaxNodeExp, SyntaxKind.ExpressionStatement);
        }

        // Expression Statement
        private static void AnalyzeSyntaxNodeExp(SyntaxNodeAnalysisContext context)
        {

            var expression = (ExpressionStatementSyntax)context.Node;

            var lastNode = expression.ChildNodes().Last();
            var invocation = lastNode as InvocationExpressionSyntax;
            if (invocation == null)
                return;

            var methodSymbol = context
                .SemanticModel
                .GetSymbolInfo(invocation, context.CancellationToken)
                .Symbol as IMethodSymbol;
            if (!methodSymbol.IsGenericMethod || methodSymbol.Name == "Select")
                return;

            var returnTypeSymbol = methodSymbol.ReturnType;
            var genericTypeSymbol = ((INamedTypeSymbol)returnTypeSymbol).TypeArguments.First();
            bool genericTypeHasProperties = genericTypeSymbol.GetMembers()
                .Where(m => m.Kind == SymbolKind.Property).Any();
            if (!genericTypeHasProperties)
                return;

            var compilation = context.Compilation;
            var iQueryableType = compilation.GetTypeByMetadataName("System.Linq.IQueryable`1");

            var areEqual = SymbolEqualityComparer.Default.Equals(iQueryableType, returnTypeSymbol.OriginalDefinition);
            if (!areEqual)
                return;

            var location = expression.GetLocation();

            var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), methodSymbol.Name);

            context.ReportDiagnostic(diagnostic);

        }

        // Single Invocation Expression
        private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {

            var invocation = (InvocationExpressionSyntax)context.Node;

            var methodSymbol = context
                .SemanticModel
                .GetSymbolInfo(invocation, context.CancellationToken)
                .Symbol as IMethodSymbol;
            if (!methodSymbol.IsGenericMethod)
                return;

            var returnTypeSymbol = methodSymbol.ReturnType;

            var compilation = context.Compilation;
            var iQueryableType = compilation.GetTypeByMetadataName("System.Linq.IQueryable`1");

            var areEqual = SymbolEqualityComparer.Default.Equals(iQueryableType, returnTypeSymbol.OriginalDefinition);
            if (!areEqual)
                return;

            var location = invocation.GetLocation();

            var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), methodSymbol.Name);

            context.ReportDiagnostic(diagnostic);

        }
    }
}
