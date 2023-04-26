using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Analyzer1
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Analyzer1CodeFixProvider)), Shared]
    public class Analyzer1CodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Analyzer1Analyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var methodDeclaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.CodeFixTitle,
                    createChangedDocument: c => MoveBooleanParametersIntoOptionsAsync(context.Document, methodDeclaration, c),
                    equivalenceKey: CodeFixResources.CodeFixTitle),
                diagnostic);


        }

        private async Task<Document> MoveBooleanParametersIntoOptionsAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
        {
            var booleanParameters = methodDeclaration.ParameterList.Parameters
                .Where(p => p.Type.IsKind(SyntaxKind.BoolKeyword))
                .ToList();

            var optionsClassName = $"{methodDeclaration.Identifier.Text}Options";
            var optionsClass = SyntaxFactory.ClassDeclaration(optionsClassName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(booleanParameters.Select(p =>
                    SyntaxFactory.PropertyDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)), p.Identifier)
                        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                        .AddAccessorListAccessors(
                            SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                            SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))));

            var newMethodDeclaration = methodDeclaration
                .WithParameterList(methodDeclaration.ParameterList.WithParameters())
                .WithBody(methodDeclaration.Body.WithStatements(new[]
                {
                    SyntaxFactory.LocalDeclarationStatement(
                            SyntaxFactory.VariableDeclaration(
                                SyntaxFactory.IdentifierName(optionsClassName),
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.VariableDeclarator(
                                        SyntaxFactory.Identifier("options"),
                                        null,
                                        SyntaxFactory.EqualsValueClause(
                                            SyntaxFactory.ObjectCreationExpression(
                                                    SyntaxFactory.IdentifierName(optionsClassName))
                                                .WithArgumentList(
                                                    SyntaxFactory.ArgumentList()))))))
                        .NormalizeWhitespace(),
                    SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.InvocationExpression(
                                methodDeclaration.Identifier,
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.IdentifierName("options"))))))
                        .NormalizeWhitespace()
                }));
            return new Document();
        }
    }
}
