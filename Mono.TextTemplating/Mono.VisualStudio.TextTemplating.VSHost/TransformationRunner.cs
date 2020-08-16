// 
// TransformationRunner.cs
//  
// Author:
//       Kenneth Carter <kccarter32@gmail.com>
// 
// Copyright (c) 2020 SubSonic-Core. (https://github.com/SubSonic-Core)
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
using System.Globalization;
using System.Reflection;
#if NETSTANDARD
using System.Runtime.Loader;
#endif
using Mono.TextTemplating;

namespace Mono.VisualStudio.TextTemplating.VSHost
{
	[Serializable]
	public abstract class TransformationRunner
		: IProcessTransformationRunner
		, IDisposable
	{
		[ThreadStatic]
		static CompiledTemplate compiledTemplate;
		[ThreadStatic]
		static ITextTemplatingEngineHost host;
		bool disposedValue;

		public TransformationRunFactory Factory { get; private set; }
		public Guid RunnerId { get; private set; }

		protected static CompiledTemplate CompiledTemplate { get => compiledTemplate; set => compiledTemplate = value; }
		protected TemplateSettings Settings { get; private set; }
#pragma warning disable CA1822 // Mark members as static
		public ITextTemplatingEngineHost Host { get => host; private set => host = value; }
#pragma warning restore CA1822 // Mark members as static

		public TemplateErrorCollection Errors { get; private set; }

		public TransformationRunner(TransformationRunFactory factory, Guid id)
		{
			Factory = factory ?? throw new ArgumentNullException (nameof (factory)); // this tags the runner with the run factory
			RunnerId = id;

			Errors = new TemplateErrorCollection ();
		}

#if NETSTANDARD
		public abstract Assembly LoadFromAssemblyName (AssemblyName assemblyName);

		protected abstract void Unload ();

		protected static Assembly ResolveReferencedAssemblies (AssemblyLoadContext context, AssemblyName assemblyName)
		{
			if (context == null) {
				throw new ArgumentNullException (nameof (context));
			}

			if (assemblyName == null) {
				throw new ArgumentNullException (nameof (assemblyName));
			}

			foreach (string assemblyPath in CompiledTemplate.AssemblyFiles) {
				if (assemblyName.Name == System.IO.Path.GetFileNameWithoutExtension (assemblyPath)) {
					return context.LoadFromAssemblyPath (assemblyPath);
				}
			}

			string filePath = host.ResolveAssemblyReference (assemblyName.Name);

			if (System.IO.File.Exists (filePath)) {
				return context.LoadFromAssemblyPath (filePath);
			}

			return null;
		}
#else
		Assembly ResolveReferencedAssemblies (object sender, ResolveEventArgs args)
		{
#pragma warning disable IDE0007 // Use implicit type
			AssemblyName asmName = new AssemblyName (args.Name);
#pragma warning restore IDE0007 // Use implicit type
			foreach (var asmFile in CompiledTemplate.AssemblyFiles) {
				if (asmName.Name == System.IO.Path.GetFileNameWithoutExtension (asmFile)) {
					return Assembly.LoadFrom (asmFile);
				}
			}

			var path = Host.ResolveAssemblyReference (asmName.Name + ".dll");

			if (System.IO.File.Exists (path)) {
				return Assembly.LoadFrom (path);
			}

			return null;
		}
#endif

		public virtual ITextTemplatingCallback PerformTransformation ()
		{
			if (host is ProcessEngineHost engineHost) {
				string errorOutput = VsTemplatingErrorResources.ErrorOutput;

				if (CompiledTemplate == null) {
					LogError (VsTemplatingErrorResources.ErrorInitializingTransformationObject, false);

					engineHost.SetTemplateOutput (errorOutput);

					return engineHost.Callback;
				}

				object transform = null;

				try {
#if NETSTANDARD
					if (CompiledTemplate.Load (this)) {
						transform = CreateTextTransformation (Settings, Host, CompiledTemplate.Assembly);

						if (transform != null) {
							CompiledTemplate.SetTextTemplatingEngineHost (Host);

							engineHost.SetTemplateOutput (CompiledTemplate.Process (transform)?.Trim () ?? errorOutput);
						}
						else {
							engineHost.SetTemplateOutput (errorOutput);
						}
					}
#else
					AppDomain.CurrentDomain.AssemblyResolve += ResolveReferencedAssemblies;

					if (CompiledTemplate.Load ()) {
						transform = CreateTextTransformation (Settings, Host, CompiledTemplate.Assembly);

						if (transform != null) {
							CompiledTemplate.SetTextTemplatingEngineHost (Host);

							engineHost.SetTemplateOutput (CompiledTemplate.Process (transform)?.Trim () ?? errorOutput);
						} else {
							engineHost.SetTemplateOutput (errorOutput);
						}
					}
#endif
				}
				catch (Exception ex) {
					if (TemplatingEngine.IsCriticalException (ex)) {
						throw;
					}
					engineHost.SetTemplateOutput (errorOutput);
					LogError (ex.ToString (), false);
				}
				finally {

#if NETSTANDARD
					// netcore 3.x can unload an assembly context, but that only happens after the assembly is
					// no longer in focus, if the compiled template is cached, is it out of focus?
					Dispose (!Settings.CachedTemplates);
#else
					AppDomain.CurrentDomain.AssemblyResolve -= ResolveReferencedAssemblies;
#endif
					engineHost.LogErrors (Errors);

					if (transform is IDisposable disposable) {
						disposable.Dispose ();
					}
					CompiledTemplate?.Dispose ();
					CompiledTemplate = null;
				}
				return engineHost.Callback;
			}
			throw new NotSupportedException (string.Format(CultureInfo.CurrentCulture, VsTemplatingErrorResources.EngineHostNotSubClassOfProcessEngineHost, host.GetType().Name, typeof(ProcessEngineHost).FullName));
		}

		static PropertyInfo GetDerivedProperty (Type transformType, string propertyName)
		{
			while(transformType != typeof(object) && transformType != null) {
				PropertyInfo property = transformType.GetProperty (propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				if (property != null) {
					return property;
				}
				transformType = transformType.BaseType;
			}
			return null;
		}

		protected virtual object CreateTextTransformation(TemplateSettings settings, ITextTemplatingEngineHost host, Assembly assembly) {
			object success = null;

			if (settings == null) {
				throw new ArgumentNullException (nameof (settings));
			}

			if (host == null) {
				throw new ArgumentNullException (nameof (host));
			}

			if (assembly == null) {
				throw new ArgumentNullException (nameof (assembly));
			}

			try {
				Type type;

				var result = assembly.CreateInstance (settings.GetFullName ());

				if (result != null) {
					type = result.GetType ();

					if (settings.HostPropertyOnBase || settings.HostSpecific) {
						try {
							PropertyInfo property = type.GetProperty ("Host");

							if (property != null) {
								property?.SetValue (result, host, null);
							}
							else {
								LogError (string.Format(CultureInfo.CurrentCulture, VsTemplatingErrorResources.HostPropertyNotFound, settings.GetHostType().Name), false);
							}	
						}
						catch(Exception hostException) {
							if (TemplatingEngine.IsCriticalException(hostException)) {
								throw;
							}
							LogError (string.Format (CultureInfo.CurrentCulture, VsTemplatingErrorResources.ExceptionSettingHost, settings.GetFullName ()), false);
						}
					}

					try {
						if (host is ITextTemplatingSessionHost sessionHost &&
							sessionHost.Session != null) {
							PropertyInfo property = GetDerivedProperty (type, nameof (TextTransformation.Session));

							property?.SetValue (result, sessionHost.Session, null);
						}
						else {
							throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, VsTemplatingErrorResources.SessionHostSessionNotInitialized, settings.GetFullName()));
						}
					}
					catch (Exception sessionException) {
						if (TemplatingEngine.IsCriticalException (sessionException)) {
							throw;
						}
						LogError (string.Format (CultureInfo.CurrentCulture, VsTemplatingErrorResources.ExceptionSettingSession, sessionException), false);
					}
					success = result;

				} else {
					LogError (VsTemplatingErrorResources.ExceptionInstantiatingTransformationObject, false);
				}
			}
			catch(Exception instantiatingException) {
				if (TemplatingEngine.IsCriticalException (instantiatingException)) {
					throw;
				}
				LogError (VsTemplatingErrorResources.ExceptionInstantiatingTransformationObject + string.Format(CultureInfo.CurrentCulture, VsTemplatingErrorResources.Exception, instantiatingException), false);
				success = null;
			}
			return success;
		}

		public virtual bool PrepareTransformation (ParsedTemplate pt, string content, ITextTemplatingEngineHost host, TemplateSettings settings)
		{
			Host = host ?? throw new ArgumentNullException (nameof (host));
			Settings = settings ?? throw new ArgumentNullException (nameof (settings));

			try {
				Settings.Assemblies.Add (base.GetType ().Assembly.Location);
				Settings.Assemblies.Add (typeof (ITextTemplatingEngineHost).Assembly.Location);
				CompiledTemplate = LocateAssembly (pt, content);
			}
			catch(Exception ex) {
				if (TemplatingEngine.IsCriticalException (ex)) {
					throw;
				}
				LogError (string.Format (CultureInfo.CurrentCulture, VsTemplatingErrorResources.Exception, ex), false);
			}
			return CompiledTemplate != null;
		}

		CompiledTemplate LocateAssembly (ParsedTemplate pt, string content)
		{
			CompiledTemplate compiledTemplate = null;

			if (Settings.CachedTemplates) {
				compiledTemplate = CompiledTemplateCache.Find (Settings.GetFullName ());
			}
			if (compiledTemplate == null) {
				compiledTemplate = Compile (pt, content);
				if (Settings.CachedTemplates && compiledTemplate != null) {
					CompiledTemplateCache.Insert (Settings.GetFullName (), compiledTemplate);
				}
			}
			return compiledTemplate;
		}

		CompiledTemplate Compile (ParsedTemplate pt, string content)
		{
			CompiledTemplate compiledTemplate = Factory.Engine.CompileTemplate (pt, content, Host, Settings);

			if (Host is ProcessEngineHost engineHost &&
				engineHost.Errors.HasErrors) {
				Errors.AddRange (engineHost.Errors);
			}

			if (Settings.CachedTemplates) {
				// we will resolve loading of assemblies through the run factory for cached templates
				compiledTemplate?.Dispose ();
			}

			return compiledTemplate;
		}

		protected Assembly AttemptAssemblyLoad(AssemblyName assembly)
		{
			if (assembly == null) {
				throw new ArgumentNullException (nameof (assembly));
			}
			try {
				return Assembly.LoadFrom (assembly.CodeBase);
			}
			catch(Exception ex) {
				if (TemplatingEngine.IsCriticalException(ex)) {
					throw;
				}
				LogError (string.Format (CultureInfo.CurrentCulture, VsTemplatingErrorResources.AssemblyLoadError, assembly.Name, ex), false);
				return null;
			}
		}

		public void ClearErrors()
		{
			Errors.Clear ();
		}

		protected void LogError(string message, bool isWarning)
		{
			Errors.Add (new TemplateError (message, new Location (Host?.TemplateFile ?? string.Empty)) {
				IsWarning = isWarning
			});
		}

		protected void LogError(string message, bool isWarning, string filename, int line, int column)
		{
			Errors.Add (new TemplateError (message, new Location (filename, line, column)) {
				IsWarning = isWarning
			});
		}

		protected virtual void Dispose (bool disposing)
		{
			if (!disposedValue) {
				if (disposing) {
					// TODO: dispose managed state (managed objects)
					compiledTemplate?.Dispose ();
					compiledTemplate = null;
				}

				// TODO: free unmanaged resources (unmanaged objects) and override finalizer
				// TODO: set large fields to null
				disposedValue = true;
			}
		}

		// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~TransformationRunner()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose ()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose (disposing: true);
			GC.SuppressFinalize (this);
		}
	}
}
