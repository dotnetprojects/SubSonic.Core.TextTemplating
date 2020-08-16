// 
// CompiledTemplate.cs
//  
// Author:
//       Nathan Baulch <nathan.baulch@gmail.com>
//
// Modified By:
//       Kenneth Carter <kccarter32@gmail.com>
// 
// Copyright (c) 2009 Nathan Baulch
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
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Mono.VisualStudio.TextTemplating;
using Mono.VisualStudio.TextTemplating.VSHost;
using System.Linq;
#if NETSTANDARD
using System.Runtime.Loader;
#endif

namespace Mono.TextTemplating
{
	[Serializable]
	public sealed class CompiledTemplate :
#if FEATURE_APPDOMAINS
		MarshalByRefObject,
#endif
		IDisposable
	{
		Type transformType;
		internal object textTransformation;
		readonly CultureInfo culture;
		ITextTemplatingEngineHost host;

		public CompiledTemplate (CompilerResults results, string fullName, ITextTemplatingEngineHost host, CultureInfo culture, string [] assemblyFiles)
		{
			if (results == null) {
				throw new ArgumentNullException (nameof (results));
			}

			this.culture = culture ?? CultureInfo.CurrentCulture;
			AssemblyFiles = assemblyFiles ?? throw new ArgumentNullException (nameof (assemblyFiles));

			SetTextTemplatingEngineHost (host);
			Load (results, fullName);
		}

#if FEATURE_APPDOMAINS
		static AppDomain CurrentDomain => AppDomain.CurrentDomain;
#endif

		/// <summary>
		/// Get the compiled assembly
		/// </summary>
		public Assembly Assembly { get; private set; }

		public AssemblyName AssemblyName { get; private set; }
		/// <summary>
		/// get a list of required assemblies
		/// </summary>
		public IEnumerable<string> AssemblyFiles { get; private set; }

#if NETSTANDARD
		public bool Load (TransformationRunner runner)
		{
			bool success = false;

			if (runner == null) {
				throw new ArgumentNullException (nameof (runner));
			}

			try {
				if (AssemblyName != null) {
					Assembly = runner.LoadFromAssemblyName (AssemblyName);
				}

				success = true;
			}
			catch (Exception) {
				throw;
			}
			return success;
		}
#else
		public bool Load ()
		{
			bool success = false;

			try {
				if (AssemblyName != null) {
					Assembly = Assembly.Load (AssemblyName);
				}

				success = true;
			}
			catch (Exception ex) {
				if (TemplatingEngine.IsCriticalException (ex)) {
					throw;
				}
			}
			return success;
		}
#endif


	void Load (CompilerResults results, string fullName)
		{
#if FEATURE_APPDOMAINS
			CurrentDomain.AssemblyResolve += ResolveReferencedAssemblies;
#endif
			//results.CompiledAssembly doesn't work on .NET core, it throws a cryptic internal error
			//use Assembly.LoadFile instead
			//for debugging we need the assembly
			Assembly = Assembly.LoadFile (results.PathToAssembly);
			// grab the assembly name
			AssemblyName = Assembly.GetName ();

			AssemblyFiles = AssemblyFiles.Union (new[] { results.PathToAssembly }).ToArray();

			transformType = Assembly.GetType (fullName);
			//MS Templating Engine does not look on the type itself, 
			//it checks only that required methods are exists in the compiled type 
			textTransformation = Activator.CreateInstance (transformType);

			Type hostType = null;

			if (host is TemplateGenerator gen) {
				hostType = gen.SpecificHostType;
			}

			var hostProp = transformType.GetProperty ("Host", hostType ?? typeof (ITextTemplatingEngineHost));

			if (hostProp != null && hostProp.CanWrite) {
				hostProp.SetValue (textTransformation, host, null);
			}

			if (host is ITextTemplatingSessionHost sessionHost) {
				//FIXME: should we create a session if it's null?
				var sessionProp = transformType.GetProperty ("Session", typeof (IDictionary<string, object>));
				sessionProp.SetValue (textTransformation, sessionHost.Session, null);
			}
		}

		public void SetTextTemplatingEngineHost(ITextTemplatingEngineHost host)
		{
			this.host = host ?? throw new ArgumentNullException (nameof (host));
		}

		public string Process()
		{
			return Process (textTransformation);
		}

		public string Process (object textTransformation)
		{
			if (textTransformation == null) {
				throw new ArgumentNullException (nameof (textTransformation));
			}

			try {
				var ttType = textTransformation.GetType ();

				var errorProp = ttType.GetProperty ("Errors", BindingFlags.Instance | BindingFlags.NonPublic);
				if (errorProp == null) {
					throw new ArgumentException ("Template must have 'Errors' property");
				}
				var errorMethod = ttType.GetMethod ("Error", new Type[] { typeof (string) });
				if (errorMethod == null) {
					throw new ArgumentException ("Template must have 'Error(string message)' method");
				}

				var errors = (CompilerErrorCollection)errorProp.GetValue (textTransformation, null);
				errors.Clear ();

				//set the culture
				if (culture != null) {
					ToStringHelper.FormatProvider = culture;
				} else {
					ToStringHelper.FormatProvider = CultureInfo.InvariantCulture;
				}

				string output = null;

				var initMethod = ttType.GetMethod ("Initialize");
				var transformMethod = ttType.GetMethod ("TransformText");

				if (initMethod == null) {
					errorMethod.Invoke (textTransformation, new object[] { "Error running transform: no method Initialize()" });
				} else if (transformMethod == null) {
					errorMethod.Invoke (textTransformation, new object[] { "Error running transform: no method TransformText()" });
				} else {
					try {
						initMethod.Invoke (textTransformation, null);
						output = (string)transformMethod.Invoke (textTransformation, null);
					}
					catch (Exception ex) {
						if (TemplatingEngine.IsCriticalException(ex)) {
							throw;
						}
						errorMethod.Invoke (textTransformation, new object[] { "Error running transform: " + ex });
					}
				}

				host.LogErrors (errors.ToTemplateErrorCollection());

				ToStringHelper.FormatProvider = CultureInfo.InvariantCulture;

				return output;
			}
			finally {
#if FEATURE_APPDOMAINS
				CurrentDomain.AssemblyResolve -= ResolveReferencedAssemblies;
#endif
			}
		}
#if FEATURE_APPDOMAINS
		Assembly ResolveReferencedAssemblies (object sender, ResolveEventArgs args)
		{
			AssemblyName asmName = new AssemblyName (args.Name);
			foreach (var asmFile in AssemblyFiles) {
				if (asmName.Name == System.IO.Path.GetFileNameWithoutExtension (asmFile)) {
					return Assembly.LoadFrom (asmFile);
				}
			}

			var path = host.ResolveAssemblyReference (asmName.Name + ".dll");

			if (System.IO.File.Exists (path)) {
				return Assembly.LoadFrom (path);
			}

			return null;
		}
#endif

		public void Dispose ()
		{
			if (textTransformation is IDisposable disposable) {
				disposable.Dispose ();
				textTransformation = null;
			}

			if (host != null) {
				host = null;
			}
		}
	}
}
