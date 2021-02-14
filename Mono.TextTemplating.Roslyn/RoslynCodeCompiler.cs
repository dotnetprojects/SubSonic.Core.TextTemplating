// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Mono.TextTemplating.CodeCompilation;

using CodeCompiler = Mono.TextTemplating.CodeCompilation.CodeCompiler;

namespace Mono.TextTemplating
{
	class RoslynCodeCompiler : CodeCompiler
	{
		readonly RuntimeInfo runtime;

		public RoslynCodeCompiler (RuntimeInfo runtime)
		{
			this.runtime = runtime;
		}

		public override async Task<CodeCompilerResult> CompileFile (
			CodeCompilerArguments arguments,
			TextWriter log,
			CancellationToken token)
		{
			var references = new List<MetadataReference> ();
			foreach (var assemblyReference in AssemblyResolver.GetResolvedReferences (runtime, arguments.AssemblyReferences)) {
				references.Add (MetadataReference.CreateFromFile (assemblyReference));
				try {
					Assembly.LoadFrom (assemblyReference);
				} catch (Exception) { }
			}

			var syntaxTrees = new List<SyntaxTree> ();
			foreach (var sourceFile in arguments.SourceFiles) {
				using var stream = File.OpenRead (sourceFile);
				var sourceText = SourceText.From (stream, Encoding.UTF8, canBeEmbedded: true);
				syntaxTrees.Add (CSharpSyntaxTree.ParseText (sourceText, path: sourceFile ));
			}

			var compilationoptions = new CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary);
			if (arguments.Debug)
				compilationoptions = compilationoptions.WithOptimizationLevel (OptimizationLevel.Debug);

			var compilation = CSharpCompilation.Create (
				"GeneratedTextTransformation",
				syntaxTrees,
				references,
				compilationoptions
			);

			EmitOptions emitOptions = null;
			List<EmbeddedText> embeddedTexts = null;
			string pdbPath = null;
			if (arguments.Debug) {
				pdbPath = Path.ChangeExtension(arguments.OutputPath, "pdb");
				embeddedTexts = syntaxTrees.Where ( st => !string.IsNullOrEmpty(st.FilePath)).Select (st => EmbeddedText.FromSource (st.FilePath, st.GetText ())).ToList ();
				emitOptions = new EmitOptions (debugInformationFormat: DebugInformationFormat.PortablePdb, pdbFilePath: pdbPath);
			}

			EmitResult result;
			using var fs = File.OpenWrite (arguments.OutputPath);
			{
				if (pdbPath != null) {
					using var pdb = File.OpenWrite (pdbPath);
					result = compilation.Emit (fs, pdbStream: pdb, options: emitOptions, embeddedTexts: embeddedTexts);
				} else {
					result = compilation.Emit (fs, options: emitOptions, embeddedTexts: embeddedTexts);
				}
			}

			if (result.Success) {
				return new CodeCompilerResult {
					Output = new List<string> (),
					Success = true,
					Errors = new List<CodeCompilerError> ()
				};
			}

			var failures = result.Diagnostics.Where (x => x.IsWarningAsError || x.Severity == DiagnosticSeverity.Error);
			var errors = failures.Select (x => {
				var location = x.Location.GetMappedLineSpan ();
				var startLinePosition = location.StartLinePosition;
				var endLinePosition = location.EndLinePosition;
				return new CodeCompilerError {
					Message = x.GetMessage (),
					Column = startLinePosition.Character,
					Line = startLinePosition.Line,
					EndLine = endLinePosition.Line,
					EndColumn = endLinePosition.Character,
					IsError = x.Severity == DiagnosticSeverity.Error,
					Origin = location.Path
				};
			}).ToList ();

			return new CodeCompilerResult {
				Success = false,
				Output = new List<string> (),
				Errors = errors
			};
		}
	}
}