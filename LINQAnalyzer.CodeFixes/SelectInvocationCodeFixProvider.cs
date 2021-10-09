using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace LINQAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SelectInvocationCodeFixProvider)), Shared]
    public class SelectInvocationCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(SelectInvocationAnalyzer.DiagnosticId); }
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

            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.SelectInvocationAddSelect,
                    createChangedDocument: c => AddMakeSelectStatementAsync(context.Document, declaration, c),
                    equivalenceKey: nameof(CodeFixResources.SelectInvocationAddSelect)),
                diagnostic);
        }

        protected async Task<Document> AddMakeSelectStatementAsync(
            Document document,
            InvocationExpressionSyntax invocationExpression,
            CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var methodSymbol = semanticModel
                .GetSymbolInfo(invocationExpression, cancellationToken)
                .Symbol as IMethodSymbol;

            var returnTypeSymbol = methodSymbol.ReturnType;

            var selectExpressionSyntax = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocationExpression,
    IdentifierName("Select"));

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
            var parameter = Parameter(
                Identifier(parameterName)
                .WithTrailingTrivia(Space));

            var initializerNodesAndTokens = new List<SyntaxNodeOrToken>();
            var publicMembers = genericTypeSymbol.GetMembers().Where(m => m.Kind == SymbolKind.Property);
            //if (!publicMembers.Any()) return;

            foreach (var property in publicMembers)
            {
                initializerNodesAndTokens.Add(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(
                            Identifier(property.Name)
                            .WithTrailingTrivia(Space)),
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(parameterName),
                            IdentifierName(
                                Identifier(property.Name))))
                    .WithOperatorToken(
                        Token(SyntaxKind.EqualsToken)
                        .WithTrailingTrivia(Space)));

                if (!SymbolEqualityComparer.Default.Equals(publicMembers.Last(), property))
                    initializerNodesAndTokens.Add(
                        Token(SyntaxKind.CommaToken)
                        .WithTrailingTrivia(Space));
            }

            var typeName = ParseTypeName(genericTypeSymbolName);

            var lambda = SimpleLambdaExpression(parameter,
                    ObjectCreationExpression(
                        typeName
                        .WithTrailingTrivia(Space))
                    .WithNewKeyword(
                        Token(SyntaxKind.NewKeyword)
                        .WithTrailingTrivia(Space))
                    .WithInitializer(
                        InitializerExpression(SyntaxKind.ObjectInitializerExpression,
                            SeparatedList<ExpressionSyntax>(initializerNodesAndTokens))
                        .WithOpenBraceToken(
                            Token(SyntaxKind.OpenBraceToken)
                            .WithTrailingTrivia(Space))
                        .WithCloseBraceToken(
                            Token(SyntaxKind.CloseBraceToken)
                            .WithLeadingTrivia(Space))))
                .WithArrowToken(
                    Token(SyntaxKind.EqualsGreaterThanToken)
                    .WithLeadingTrivia(Space)
                    .WithTrailingTrivia(Space));

            var selectInvocationExpression = InvocationExpression(selectExpressionSyntax)
                .WithArgumentList(
                ArgumentList(
                    SingletonSeparatedList(Argument(lambda))));

            // replace original invocation with this one

            var formattedLocal = selectInvocationExpression.WithAdditionalAnnotations(Formatter.Annotation);

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(invocationExpression, formattedLocal);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
