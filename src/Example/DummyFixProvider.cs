using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Example
{
	sealed class DummyFixProvider : CodeFixProvider
	{
		public const string EquivalenceKey = "Dummy EquivalenceKey";
		public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DummyAnalyzer.ID);
		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var diagnostic = context.Diagnostics[0];
			var document = context.Document;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Make Class Partial",
					c => Fix(document, diagnostic, c),
					equivalenceKey: EquivalenceKey),
				diagnostic);

			return Task.CompletedTask;
		}

		async Task<Document> Fix(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			var node = (ClassDeclarationSyntax)root.FindNode(diagnostic.Location.SourceSpan);

			return document.WithSyntaxRoot(
				root.ReplaceNode(
					node,
					node.WithModifiers(
						node.Modifiers.Add(
							SyntaxFactory.Token(SyntaxKind.PartialKeyword)
								.WithTrailingTrivia(SyntaxFactory.Space)))));
		}
	}
}
