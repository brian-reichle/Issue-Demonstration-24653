using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;

namespace Example
{
	static class Program
	{
		const string SolutionPath = @"..\..\src\Example.sln";

		static async Task Main(string[] args)
		{
			using (var cts = new CancellationTokenSource())
			{
				Console.CancelKeyPress += (sender, e) =>
				{
					e.Cancel = true;
					cts.Cancel();
				};

				await MainAsync(args, cts.Token);
			}
		}

		static async Task MainAsync(string[] args, CancellationToken cancellationToken)
		{
			using (var workspace = MSBuildWorkspace.Create())
			{
				await workspace.OpenSolutionAsync(SolutionPath, cancellationToken).ConfigureAwait(false);
				var solution = workspace.CurrentSolution;

				foreach (var projId in solution.ProjectIds)
				{
					solution = await ProcessProject(workspace, projId, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		static async Task<Solution> ProcessProject(Workspace workspace, ProjectId projId, CancellationToken cancellationToken)
		{
			var project = workspace.CurrentSolution.GetProject(projId);
			var diagnostics = await GetFilteredDiagnosticsByDocument(project, cancellationToken).ConfigureAwait(false);

			var codeAction = await FixProvider.GetFixAllProvider().GetFixAsync(
				new FixAllContext(
					diagnostics.Keys.First(),
					FixProvider,
					FixAllScope.Project,
					DummyFixProvider.EquivalenceKey,
					new[] { DummyAnalyzer.ID },
					new DiagnosticProvider(diagnostics),
					cancellationToken))
				.ConfigureAwait(false);

			var operations = await codeAction.GetOperationsAsync(cancellationToken).ConfigureAwait(false);

			foreach (var operation in operations)
			{
				operation.Apply(workspace, cancellationToken);
			}

			return project.Solution;
		}

		static async Task<Dictionary<Document, List<Diagnostic>>> GetFilteredDiagnosticsByDocument(Project project, CancellationToken cancellationToken)
		{
			var diagnostics = await GetDiagnostics(project, cancellationToken).ConfigureAwait(false);
			var result = new Dictionary<Document, List<Diagnostic>>();

			foreach (var diagnostic in diagnostics)
			{
				var document = project.Solution.GetDocument(diagnostic.Location.SourceTree);

				await FixProvider.RegisterCodeFixesAsync(new CodeFixContext(
					document,
					diagnostic,
					(action, d) =>
					{
						if (action.EquivalenceKey == DummyFixProvider.EquivalenceKey)
						{
							if (!result.TryGetValue(document, out var list))
							{
								result.Add(document, list = new List<Diagnostic>());
							}

							list.Add(diagnostic);
						}
					},
					cancellationToken));
			}

			return result;
		}

		static async Task<ImmutableArray<Diagnostic>> GetDiagnostics(Project project, CancellationToken cancellationToken)
		{
			var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

			return await compilation
				.WithAnalyzers(Analyzers, EmptyCompilationWithAnalyzersOptions)
				.GetAnalyzerDiagnosticsAsync(Analyzers, cancellationToken).ConfigureAwait(false);
		}

		static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new DummyAnalyzer());
		static readonly DummyFixProvider FixProvider = new DummyFixProvider();

		static readonly CompilationWithAnalyzersOptions EmptyCompilationWithAnalyzersOptions = new CompilationWithAnalyzersOptions(
			new AnalyzerOptions(ImmutableArray.Create<AdditionalText>()), null, true, false);

		sealed class DiagnosticProvider : FixAllContext.DiagnosticProvider
		{
			public DiagnosticProvider(Dictionary<Document, List<Diagnostic>> diagnostics) => this.diagnostics = diagnostics;

			public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
				=> Task.FromResult(diagnostics.TryGetValue(document, out var list) ? list : Enumerable.Empty<Diagnostic>());

			public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
				=> Task.FromResult(diagnostics.Values.SelectMany(x => x));

			public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
				=> Task.FromResult(diagnostics.Values.SelectMany(x => x));

			readonly Dictionary<Document, List<Diagnostic>> diagnostics;
		}
	}
}
