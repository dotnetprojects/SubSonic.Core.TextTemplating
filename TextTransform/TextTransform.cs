// 
// Main.cs
//  
// Author:
//       Mikayla Hutchinson <m.j.hutchinson@gmail.com>
// 
// Copyright (c) 2009 Novell, Inc. (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Options;

namespace Mono.TextTemplating
{
	class TextTransform
	{
		static OptionSet optionSet;

		public static int Main (string[] args)
		{
			try {
				return MainInternal(args);
			}
			catch (Exception e) {
				Console.Error.WriteLine(e);
				return -1;
			}
		}

		static int MainInternal (string[] args)
		{
			if (args.Length == 0) {
				ShowHelp (true);
			}
			
			var generator = new TemplateGenerator ();
			
			string outputFile = null, inputFile = null;
			var directives = new List<string> ();
			var parameters = new List<string> ();
			string preprocess = null;
			
			optionSet = new OptionSet () {
				{ "o=|out=", "The name of the output {file}", s => outputFile = s },
				{ "r=", "Assemblies to reference", s => generator.Refs.Add (s) },
				{ "u=", "Namespaces to import <{0:namespace}>", s => generator.Imports.Add (s) },
				{ "I=", "Paths to search for included files", s => generator.IncludePaths.Add (s) },
				{ "P=", "Paths to search for referenced assemblies", s => generator.ReferencePaths.Add (s) },
				{ "dp=", "Directive processor (name!class!assembly)", s => directives.Add (s) },
				{ "a=", "Parameters (name=value) or ([processorName!][directiveName!]name!value)", s => parameters.Add (s) },
				{ "roslyn", "Use Roslyn", s => generator.UseInProcessCompiler () },
				{ "h|?|help", "Show help", s => ShowHelp (false) },
				//		{ "k=,", "Session {key},{value} pairs", (s, t) => session.Add (s, t) },
				{ "c=", "Preprocess the template into {0:class}", (s) => preprocess = s },
			};
			
			var remainingArgs = optionSet.Parse (args);
			
			if (remainingArgs.Count != 1) {
				Console.Error.WriteLine ("No input file specified.");
				return -1;
			}
			inputFile = remainingArgs [0];
			
			if (!File.Exists (inputFile)) {
				Console.Error.WriteLine ("Input file '{0}' does not exist.", inputFile);
				return -1;
			}
			
			if (string.IsNullOrEmpty (outputFile)) {
				outputFile = inputFile;
				if (Path.HasExtension (outputFile)) {
					var dir = Path.GetDirectoryName (outputFile);
					var fn = Path.GetFileNameWithoutExtension (outputFile);
					outputFile = Path.Combine (dir, fn + ".txt");
				} else {
					outputFile = outputFile + ".txt";
				}
			}

			foreach (var par in parameters) {
				if (!generator.TryAddParameter (par)) {
					Console.Error.WriteLine ("Parameter has incorrect format: {0}", par);
					return -1;
				}
			}
			
			foreach (var dir in directives) {
				var split = dir.Split ('!');

				if (split.Length != 3) {
					Console.Error.WriteLine ("Directive must have 3 values: {0}", dir);
					return -1;
				}

				for (int i = 0; i < 3; i++) {
					string s = split [i];
					if (string.IsNullOrEmpty (s)) {
						string kind = i == 0? "name" : (i == 1 ? "class" : "assembly");
						Console.Error.WriteLine ("Directive has missing {0} value: {1}", kind, dir);
						return -1;
					}
				}

				generator.AddDirectiveProcessor (split[0], split[1], split[2]);
			}
			
			if (preprocess == null) {
				generator.ProcessTemplate (inputFile, outputFile);
				if (generator.Errors.HasErrors) {
					Console.WriteLine ("Processing '{0}' failed.", inputFile);
				}
			} else {
				string className = preprocess;
				string classNamespace = null;
				int s = preprocess.LastIndexOf ('.');
				if (s > 0) {
					classNamespace = preprocess.Substring (0, s);
					className = preprocess.Substring (s + 1);
				}

				generator.PreprocessTemplate (inputFile, className, classNamespace, outputFile, new System.Text.UTF8Encoding (encoderShouldEmitUTF8Identifier: false),
					out string language, out string[] references);
				if (generator.Errors.HasErrors) {
					Console.Write ("Preprocessing '{0}' into class '{1}.{2}' failed.", inputFile, classNamespace, className);
				}
			}

			foreach (TemplateError err in generator.Errors) {
				Console.Error.WriteLine ("{0}({1},{2}): {3} {4}", err.Location.FileName, err.Location.Line, err.Location.Column,
								   err.IsWarning ? "WARNING" : "ERROR", err.Message);
			}
			
			return generator.Errors.HasErrors? -1 : 0;
		}
		
		static void ShowHelp (bool concise)
		{
			Console.WriteLine ("TextTransform command line T4 processor");
			Console.WriteLine ("Usage: {0} [options] input-file", Path.GetFileName (Assembly.GetExecutingAssembly().Location));
			if (concise) {
				Console.WriteLine ("Use --help to display options.");
			} else {
				Console.WriteLine ("Options:");
				optionSet.WriteOptionDescriptions (Console.Out);
			}
			Console.WriteLine ();
			Environment.Exit (0);
		}
	}
}