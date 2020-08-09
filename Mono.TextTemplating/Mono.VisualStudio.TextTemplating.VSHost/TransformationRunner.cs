using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
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
	{
		
		public TransformationRunFactory Factory { get; private set; }
		public Guid RunnerId { get; private set; }

		protected CompiledTemplate CompiledTemplate { get; private set; }
		protected TemplateSettings Settings { get; private set; }
		protected ITextTemplatingEngineHost Host { get; private set; }

		public CompilerErrorCollection Errors { get; private set; }

		public TransformationRunner(TransformationRunFactory factory, Guid id)
		{
			Factory = factory ?? throw new ArgumentNullException (nameof (factory)); // this tags the runner with the run factory
			RunnerId = id;

			Errors = new CompilerErrorCollection();
		}

#if NETSTANDARD
		public abstract Assembly LoadFromAssemblyName (AssemblyName assemblyName);

		protected abstract void Unload ();

		protected Assembly ResolveReferencedAssemblies (AssemblyLoadContext context, AssemblyName assemblyName)
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

			string filePath = Host.ResolveAssemblyReference ($"{assemblyName.Name}.dll");

			if (System.IO.File.Exists (filePath)) {
				return context.LoadFromAssemblyPath (filePath);
			}

			return null;
		}
#else
		Assembly ResolveReferencedAssemblies (object sender, ResolveEventArgs args)
		{
			AssemblyName asmName = new AssemblyName (args.Name);
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

		public virtual string PerformTransformation ()
		{
			string errorOutput = VsTemplatingErrorResources.ErrorOutput;

			if (CompiledTemplate == null) {
				LogError (VsTemplatingErrorResources.ErrorInitializingTransformationObject, false);

				return errorOutput;
			}

			object transform = null;

			try {
#if NETSTANDARD
				if (CompiledTemplate.Load (this)) {
					transform = CreateTextTransformation (Settings, Host, CompiledTemplate.Assembly);

					CompiledTemplate.SetTextTemplatingEngineHost (Host);

					return CompiledTemplate.Process (transform);
				}
#else
				AppDomain.CurrentDomain.AssemblyResolve += ResolveReferencedAssemblies;

				if (CompiledTemplate.Load ()) {
					transform = CreateTextTransformation (Settings, Host, CompiledTemplate.Assembly);

					CompiledTemplate.SetTextTemplatingEngineHost (Host);

					return CompiledTemplate.Process (transform);
				}
#endif


			}
			catch (Exception ex) {
				if (TemplatingEngine.IsCriticalException (ex)) {
					throw;
				}
				LogError (ex.ToString (), false);
			}
			finally {

#if NETSTANDARD
				Unload ();
#else
				AppDomain.CurrentDomain.AssemblyResolve -= ResolveReferencedAssemblies;
#endif

				if (transform is IDisposable disposable) {
					disposable.Dispose ();
				}
				CompiledTemplate?.Dispose ();
				CompiledTemplate = null;
				Host = null;
			}

			return errorOutput;
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
								LogError (string.Format(CultureInfo.CurrentCulture, VsTemplatingErrorResources.HostPropertyNotFound, settings.HostType.Name), false);
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
			CompilerError error = new CompilerError () {
				ErrorText = message,
				IsWarning = isWarning
			};

			Errors.Add (error);
		}

		protected void LogError(string message, bool isWarning, string filename, int line, int column)
		{
			CompilerError error = new CompilerError () {
				ErrorText = message,
				IsWarning = isWarning,
				FileName = filename,
				Line = line,
				Column = column
			};

			Errors.Add (error);
		}
	}
}
