using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Example
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	sealed class DummyAnalyzer : DiagnosticAnalyzer
	{
		public const string ID = "DUMMY";

		public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
			ID, "Title", "Message", "Category", DiagnosticSeverity.Error, true);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

		public override void Initialize(AnalysisContext context)
			=> context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);

		void Analyze(SyntaxNodeAnalysisContext context)
		{
			var classDecl = (ClassDeclarationSyntax)context.Node;

			foreach (var modifier in classDecl.Modifiers)
			{
				if (modifier.Kind() == SyntaxKind.PartialKeyword)
				{
					return;
				}
			}

			context.ReportDiagnostic(Diagnostic.Create(Rule, classDecl.Keyword.GetLocation()));
		}
	}
}
