using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LINQAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LINQAnalyzerCodeFixProvider)), Shared]
    public class LINQAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(LINQAnalyzerAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.CodeFixTitle,
                    createChangedDocument: c => AddMakeSelectStatementAsync(context.Document, declaration, c),
                    equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
                diagnostic);
        }

        private async Task<Document> AddMakeSelectStatementAsync(Document document, InvocationExpressionSyntax invocationExpression, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var methodSymbol = semanticModel
                .GetSymbolInfo(invocationExpression, cancellationToken)
                .Symbol as IMethodSymbol;

            var returnTypeSymbol = methodSymbol.ReturnType;

            var selectExpressionSyntax = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocationExpression,
    SyntaxFactory.IdentifierName("Select"));

            var namedReturnTypeSymbol = (INamedTypeSymbol)returnTypeSymbol;

            var genericTypeSymbol = (INamedTypeSymbol)namedReturnTypeSymbol.TypeArguments.First();
            var genericTypeSymbolName = genericTypeSymbol.ToMinimalDisplayString(semanticModel,
                0,
                SymbolDisplayFormat.MinimallyQualifiedFormat);
            genericTypeSymbolName = genericTypeSymbol.Name;

            // should check if an identifier with this name already exists in the enclosing scope.
            // if it does. add more letters from the class name and check again
            // until the entire class name is exhausted?
            var parameterName = genericTypeSymbol.Name.ToLower().Substring(0, 1);
            var parameter = SyntaxFactory.Parameter(
                SyntaxFactory.Identifier(parameterName)
                .WithTrailingTrivia(SyntaxFactory.Space));

            var initializerNodesAndTokens = new List<SyntaxNodeOrToken>();
            var publicMembers = genericTypeSymbol.GetMembers().Where(m => m.Kind == SymbolKind.Property);
            //if (!publicMembers.Any()) return;

            foreach (var property in publicMembers)
            {
                initializerNodesAndTokens.Add(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(
                            SyntaxFactory.Identifier(property.Name)
                            .WithTrailingTrivia(SyntaxFactory.Space)),
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName(parameterName),
                            SyntaxFactory.IdentifierName(
                                SyntaxFactory.Identifier(property.Name))))
                    .WithOperatorToken(
                        SyntaxFactory.Token(SyntaxKind.EqualsToken)
                        .WithTrailingTrivia(SyntaxFactory.Space)));

                if (!SymbolEqualityComparer.Default.Equals(publicMembers.Last(), property))
                    initializerNodesAndTokens.Add(
                        SyntaxFactory.Token(SyntaxKind.CommaToken)
                        .WithTrailingTrivia(SyntaxFactory.Space));
            }

            var typeName = SyntaxFactory.ParseTypeName(genericTypeSymbolName);

            var lambda = SyntaxFactory.SimpleLambdaExpression(parameter,
                    SyntaxFactory.ObjectCreationExpression(
                        typeName
                        .WithTrailingTrivia(SyntaxFactory.Space))
                    .WithNewKeyword(
                        SyntaxFactory.Token(SyntaxKind.NewKeyword)
                        .WithTrailingTrivia(SyntaxFactory.Space))
                    .WithInitializer(
                        SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression,
                            SyntaxFactory.SeparatedList<ExpressionSyntax>(initializerNodesAndTokens))
                        .WithOpenBraceToken(
                            SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                            .WithTrailingTrivia(SyntaxFactory.Space))
                        .WithCloseBraceToken(
                            SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                            .WithLeadingTrivia(SyntaxFactory.Space))))
                .WithArrowToken(
                    SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken)
                    .WithLeadingTrivia(SyntaxFactory.Space)
                    .WithTrailingTrivia(SyntaxFactory.Space));

            var selectInvocationExpression = SyntaxFactory.InvocationExpression(selectExpressionSyntax)
                .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(SyntaxFactory.Argument(lambda))));

            // replace original invocation with this one

            var formattedLocal = selectInvocationExpression.WithAdditionalAnnotations(Formatter.Annotation);

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = oldRoot.ReplaceNode(invocationExpression, formattedLocal);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
