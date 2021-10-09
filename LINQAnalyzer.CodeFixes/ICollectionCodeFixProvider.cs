using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace LINQAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SelectInvocationCodeFixProvider)), Shared]
    public class ICollectionCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(ICollectionAnalyzer.DiagnosticId); }
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

            SyntaxToken diagnosticToken = root.FindToken(diagnosticSpan.Start);
            var ancestorsAndSelf = diagnosticToken.Parent.AncestorsAndSelf();

            var containingNamespace = ancestorsAndSelf.OfType<NamespaceDeclarationSyntax>().Last();
            var containingClass = ancestorsAndSelf.OfType<ClassDeclarationSyntax>().First();
            var containingBlock = ancestorsAndSelf.OfType<BlockSyntax>().First();
            var expressionStatement = ancestorsAndSelf.OfType<ExpressionStatementSyntax>().First();
            var propertyInvocationExpression = ancestorsAndSelf
                .OfType<InvocationExpressionSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.ICollectionEnumToBoolColum,
                    createChangedSolution: c => MapEnumToBoolColumnsAsync(
                        context.Document,
                        containingNamespace,
                        containingClass,
                        containingBlock,
                        propertyInvocationExpression,
                        expressionStatement,
                        c),
                    equivalenceKey: nameof(CodeFixResources.ICollectionEnumToBoolColum)),
                diagnostic);
        }

        protected async Task<Solution> MapEnumToBoolColumnsAsync(
            Document document,
            NamespaceDeclarationSyntax containingNamespace,
            ClassDeclarationSyntax containingClass,
            BlockSyntax containingBlock,
            InvocationExpressionSyntax propertyInvocation,
            SyntaxNode expressionStatement,
            CancellationToken cancellationToken)
        {
            var solution = document.Project.Solution;
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var methodSymbol = semanticModel
                .GetSymbolInfo(propertyInvocation, cancellationToken)
                .Symbol as IMethodSymbol;

            ITypeSymbol entityClassSymbol = methodSymbol.ContainingType.TypeArguments.First();
            var entityClassDeclarations = await SymbolFinder.FindSourceDeclarationsAsync(
                solution,
                entityClassSymbol.Name, ignoreCase: false, cancellationToken);
            var entityClassDeclaration = entityClassDeclarations.First();

            var entityLocation = entityClassDeclaration.Locations.First();
            var entitySourceTree = entityLocation.SourceTree;
            var entityDocument = document.Project.Solution.GetDocument(entitySourceTree);
            bool equalDocuments = entityDocument.Equals(document);

            var entityRoot = await entityDocument.GetSyntaxRootAsync(cancellationToken);
            var entityNode = entityRoot.FindNode(entityLocation.SourceSpan) as ClassDeclarationSyntax;
            if (entityNode == null) // check this in the analyzer before offering the diagnostic
                return solution;

            // Required symbols
            var returnTypeSymbol = methodSymbol.ReturnType;
            var iCollectionTypeSymbol = ((INamedTypeSymbol)returnTypeSymbol).TypeArguments.FirstOrDefault();
            var enumTypeSymbol = (iCollectionTypeSymbol as INamedTypeSymbol).TypeArguments.FirstOrDefault();
            var enumMembers = enumTypeSymbol.GetMembers()
                .Where(m => m.Kind == SymbolKind.Field)
                .ToList();

            // Check if Property uses a lambda expression
            var firstPropertyArgument = propertyInvocation.ArgumentList.Arguments.First();
            var lambdaExpression = firstPropertyArgument.Expression as SimpleLambdaExpressionSyntax;
            if (lambdaExpression == null)
                return solution;

            string prefix = "Is";
            string infix = "To";
            var classPropertyNode = (lambdaExpression.Body as MemberAccessExpressionSyntax).ChildNodes().Last();
            var propertyName = classPropertyNode.GetText().ToString();
            var splitPropertyName = Regex.Split(propertyName, "(?<!^)(?=[A-Z])");

            // Save initial nodes
            var originalExpressionStatement = expressionStatement;
            var originalBlock = containingBlock;
            var originalClass = containingClass;

            // Remove ancestral expression statement // TODO replace with .Ignore(...)
            SyntaxNode root;
            if (equalDocuments)
                root = entityRoot;
            else
                root = await document.GetSyntaxRootAsync(cancellationToken);
            root = root.TrackNodes(expressionStatement, containingBlock, containingClass, containingNamespace);
            expressionStatement = root.GetCurrentNode(expressionStatement);
            root = root.RemoveNode(expressionStatement, SyntaxRemoveOptions.KeepNoTrivia);

            // Add interface declaration
            var interfaceName = "I";
            if (splitPropertyName.Length > 1)
                interfaceName += $"{splitPropertyName.Last()}{splitPropertyName.First()}";
            else
                interfaceName += splitPropertyName.First();

            var interfaceSyntax = InterfaceDeclaration(interfaceName)
                        .WithModifiers(
                            TokenList(
                                Token(SyntaxKind.PublicKeyword)))
                        .WithMembers(GetInterfaceMembers(
                            prefix,
                            enumTypeSymbol,
                            enumMembers,
                            iCollectionTypeSymbol,
                            propertyName,
                            infix,
                            splitPropertyName.First(),
                            splitPropertyName.Last()))
                        .NormalizeWhitespace();

            // Make entity class implement interface
            if (equalDocuments)
            {
                entityRoot = root;
                entityNode = entityRoot.FindNode(entityLocation.SourceSpan) as ClassDeclarationSyntax;
            }

            var newEntityNode = entityNode.AddBaseListTypes(
                SimpleBaseType(
                    IdentifierName(interfaceName)));
            var boolMembers = GetBoolProperties(prefix, enumMembers, true, true);
            newEntityNode = newEntityNode.AddMembers(boolMembers.ToArray()).NormalizeWhitespace();
            entityRoot = entityRoot.ReplaceNode(entityNode, newEntityNode);

            if (equalDocuments)
                root = entityRoot;

            // Add interface to containing class declaration
            containingNamespace = root.GetCurrentNode(containingNamespace);
            root = root.ReplaceNode(containingNamespace, containingNamespace.AddMembers(interfaceSyntax).NormalizeWhitespace());

            solution = solution.WithDocumentSyntaxRoot(document.Id, root);
            if (!equalDocuments)
                solution = solution.WithDocumentSyntaxRoot(entityDocument.Id, entityRoot);

            return solution;
        }

        protected static SyntaxList<MemberDeclarationSyntax> GetInterfaceMembers(
            string prefix,
            ITypeSymbol enumTypeSymbol,
            List<ISymbol> enumMembers,
            ITypeSymbol iCollectionTypeSymbol,
            string propertyName,
            string infix,
            string first,
            string last)
        {
            List<MemberDeclarationSyntax> interfaceProperties = GetBoolProperties(prefix, enumMembers);

            string identifierName = enumTypeSymbol.Name.FirstCharToLowerCase();

            interfaceProperties.Add(
                MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.BoolKeyword)),
                    Identifier($"{prefix}{first}{infix}{last}"))
                .WithParameterList(
                    ParameterList(
                        SingletonSeparatedList(
                            Parameter(
                                Identifier(identifierName))
                            .WithType(
                                IdentifierName(enumTypeSymbol.Name)))))
                .WithBody(
                    Block(
                        SingletonList(
                            (StatementSyntax)ReturnStatement(
                                SwitchExpression(
                                    IdentifierName(identifierName))
                                .WithArms(
                                    GetSwitchExpressionArms(
                                        enumTypeSymbol,
                                        enumMembers,
                                        propertyName,
                                        prefix,
                                        identifierName)))
                            .NormalizeWhitespace()))));

            interfaceProperties.Add(
                PropertyDeclaration(
                    GenericName(
                        Identifier(iCollectionTypeSymbol.Name))
                    .WithTypeArgumentList(
                        TypeArgumentList(
                            SingletonSeparatedList<TypeSyntax>(
                                IdentifierName(enumTypeSymbol.Name)))),
                    Identifier(propertyName))
                .WithAccessorList(
                    AccessorList(
                        SingletonList(
                            AccessorDeclaration(
                                SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(
                                Token(SyntaxKind.SemicolonToken))))));

            interfaceProperties.Add(
                MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)),
                    Identifier($"Add{first}{last}"))
                .WithParameterList(
                    ParameterList(
                        SingletonSeparatedList(
                            Parameter(
                                Identifier(enumTypeSymbol.Name.FirstCharToLowerCase()))
                            .WithType(
                                IdentifierName(enumTypeSymbol.Name)))))
                .WithBody(
                    Block(
                        SingletonList<StatementSyntax>(
                            ExpressionStatement(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(propertyName),
                                        IdentifierName("Add")))
                                .WithArgumentList(
                                    ArgumentList(
                                        SingletonSeparatedList(
                                            Argument(
                                                IdentifierName(identifierName)))))))))
                );

            return List(interfaceProperties.ToArray());
        }

        protected static List<MemberDeclarationSyntax> GetBoolProperties(
            string prefix,
            List<ISymbol> enumMembers,
            bool withSetters = false,
            bool withPublicAccess = false)
        {
            var interfaceProperties = new List<MemberDeclarationSyntax>();
            foreach (var enumMember in enumMembers)
            {
                interfaceProperties.Add(
                    PropertyDeclaration(
                        PredefinedType(
                            Token(SyntaxKind.BoolKeyword)),
                        Identifier($"{prefix}{enumMember.Name}"))
                    .WithAccessorList(GetAccessorsList(withSetters))
                    .WithModifiers(
                            TokenList(
                                Token(SyntaxKind.PublicKeyword))));
            }

            return interfaceProperties;
        }

        protected static AccessorListSyntax GetAccessorsList(bool withSetters)
        {
            List<AccessorDeclarationSyntax> nodes = new List<AccessorDeclarationSyntax> {
                            AccessorDeclaration(
                                SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(
                                Token(SyntaxKind.SemicolonToken))};
            if (withSetters)
                nodes.Add(
                    AccessorDeclaration(
                        SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken)));

            return AccessorList(List(nodes));
        }

        protected static SeparatedSyntaxList<SwitchExpressionArmSyntax> GetSwitchExpressionArms(
            ITypeSymbol enumTypeSymbol,
            List<ISymbol> enumMembers,
            string propertyName,
            string prefix,
            string identifierName)
        {
            var syntaxNodeOrTokens = new List<SyntaxNodeOrToken>();
            foreach (var enumMember in enumMembers)
            {
                syntaxNodeOrTokens.Add(
                    SwitchExpressionArm(
                        ConstantPattern(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(enumTypeSymbol.Name),
                                IdentifierName(enumMember.Name))),
                        IdentifierName($"{prefix}{enumMember.Name}"))
                    );
                syntaxNodeOrTokens.Add(Token(SyntaxKind.CommaToken));
            }

            syntaxNodeOrTokens.Add(
                SwitchExpressionArm(
                    DiscardPattern(),
                    LiteralExpression(SyntaxKind.FalseLiteralExpression)));
            syntaxNodeOrTokens.Add(Token(SyntaxKind.CommaToken));


            return SeparatedList<SwitchExpressionArmSyntax>(syntaxNodeOrTokens);
        }
    }
}