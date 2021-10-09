using LINQAnalyzer.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace LINQAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ICollectionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = Diagnostics.LA0002;

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ICollectionAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ICollectionAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ICollectionAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
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
               (c,x) => AnalyzeEnumICollectionProperty(c,x),
           };

        protected static void AnalyzeSyntaxNodeExpression(SyntaxNodeAnalysisContext context)
        {
            var expression = (ExpressionStatementSyntax)context.Node;
            foreach (var analysisAction in ExpressionStatementAnalyzeActions)
                analysisAction.Invoke(context, expression);
        }

        protected static void AnalyzeEnumICollectionProperty(SyntaxNodeAnalysisContext context,
            ExpressionStatementSyntax expression)
        {
            if (!AnalyzerUtils.LastNodeIsInvocationExpressionSyntax(expression, out var invocationExpressionSyntax))
                return;

            var methodSymbol = context
                .SemanticModel
                .GetSymbolInfo(invocationExpressionSyntax, context.CancellationToken)
                .Symbol as IMethodSymbol;
            if (methodSymbol == null || !methodSymbol.IsGenericMethod || methodSymbol.Name != "Property")
                return;

            var returnTypeSymbol = methodSymbol.ReturnType;
            var iCollectionTypeSymbol = ((INamedTypeSymbol)returnTypeSymbol).TypeArguments.FirstOrDefault();
            if (iCollectionTypeSymbol?.Name != nameof(ICollection))
                return;

            var enumTypeSymbol = (iCollectionTypeSymbol as INamedTypeSymbol).TypeArguments.FirstOrDefault();
            if (enumTypeSymbol?.TypeKind != TypeKind.Enum)
                return;

            var enumMembers = enumTypeSymbol.GetMembers()
                .Where(m => m.Kind == SymbolKind.Field)
                .ToList();

            if (!enumMembers.Any())
                return;

            var diagnostic = Diagnostic.Create(Rule, invocationExpressionSyntax.GetLocation(), methodSymbol.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }
}
