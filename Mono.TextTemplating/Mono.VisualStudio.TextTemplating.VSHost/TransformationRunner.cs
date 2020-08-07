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
		
		readonly TransformationRunFactory factory;		
		readonly Guid id;

		CompiledTemplate compiledTemplate;
		TemplateSettings settings;
		ITextTemplatingEngineHost host;

		public CompilerErrorCollection Errors { get; private set; }

		public TransformationRunner(TransformationRunFactory factory, Guid id)
		{
			this.factory = factory ?? throw new ArgumentNullException (nameof (factory)); // this tags the runner with the run factory
			this.id = id;

			Errors = new CompilerErrorCollection();
		}

		public Guid RunnerId { get => id; }

		public TransformationRunFactory Factory { get => factory; }
#if NETSTANDARD
		protected abstract AssemblyLoadContext GetLoadContext ();

		protected static AssemblyLoadContext GetLoadContext(Assembly assembly)
		{
			return AssemblyLoadContext.GetLoadContext (assembly);
		}

		protected abstract void Unload (AssemblyLoadContext context);

		Assembly ResolveReferencedAssemblies (AssemblyLoadContext context, AssemblyName assemblyName)
		{
			foreach (string assemblyPath in compiledTemplate.AssemblyFiles) {
				if (assemblyName.Name == System.IO.Path.GetFileNameWithoutExtension (assemblyPath)) {
					return context.LoadFromAssemblyPath (assemblyPath);
				}
			}

			string filePath = host.ResolveAssemblyReference ($"{assemblyName.Name}.dll");

			if (System.IO.File.Exists (filePath)) {
				return context.LoadFromAssemblyPath (filePath);
			}

			return null;
		}
#else
		Assembly ResolveReferencedAssemblies (object sender, ResolveEventArgs args)
		{
			AssemblyName asmName = new AssemblyName (args.Name);
			foreach (var asmFile in compiledTemplate.AssemblyFiles) {
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

		public virtual string PerformTransformation ()
		{
			string errorOutput = VsTemplatingErrorResources.ErrorOutput;

			if (compiledTemplate == null) {
				LogError (VsTemplatingErrorResources.ErrorInitializingTransformationObject, false);

				return errorOutput;
			}

			object transform = null;

			try {
#if NETSTANDARD
				if (GetLoadContext () is AssemblyLoadContext context) {
					context.Resolving += ResolveReferencedAssemblies;

					if (compiledTemplate.Load (context)) {
						transform = CreateTextTransformation (settings, host, compiledTemplate.Assembly);

						compiledTemplate.SetTextTemplatingEngineHost (host);

						return compiledTemplate.Process (transform);
					}
				}
#else
				AppDomain.CurrentDomain.AssemblyResolve += ResolveReferencedAssemblies;

				if (compiledTemplate.Load ()) {
					transform = CreateTextTransformation (settings, host, compiledTemplate.Assembly);

					compiledTemplate.SetTextTemplatingEngineHost (host);

					return compiledTemplate.Process (transform);
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
				if (GetLoadContext (compiledTemplate.Assembly) is AssemblyLoadContext context) {
					context.Resolving -= ResolveReferencedAssemblies;

					Unload (context);
				}
#else
				AppDomain.CurrentDomain.AssemblyResolve -= ResolveReferencedAssemblies;
#endif

				if (transform is IDisposable disposable) {
					disposable.Dispose ();
				}
				compiledTemplate?.Dispose ();
				compiledTemplate = null;
				host = null;
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
			this.host = host ?? throw new ArgumentNullException (nameof (host));
			this.settings = settings ?? throw new ArgumentNullException (nameof (settings));

			try {
				this.settings.Assemblies.Add (base.GetType ().Assembly.Location);
				this.settings.Assemblies.Add (typeof (ITextTemplatingEngineHost).Assembly.Location);
				compiledTemplate = LocateAssembly (pt, content);
			}
			catch(Exception ex) {
				if (TemplatingEngine.IsCriticalException (ex)) {
					throw;
				}
				LogError (string.Format (CultureInfo.CurrentCulture, VsTemplatingErrorResources.Exception, ex), false);
			}
			return compiledTemplate != null;
		}

		CompiledTemplate LocateAssembly (ParsedTemplate pt, string content)
		{
			CompiledTemplate compiledTemplate = null;

			if (settings.CachedTemplates) {
				compiledTemplate = CompiledTemplateCache.Find (settings.GetFullName ());
			}
			if (compiledTemplate == null) {
				compiledTemplate = Compile (pt, content);
				if (settings.CachedTemplates && compiledTemplate != null) {
					CompiledTemplateCache.Insert (settings.GetFullName (), compiledTemplate);
				}
			}
			return compiledTemplate;
		}

		CompiledTemplate Compile (ParsedTemplate pt, string content)
		{
			CompiledTemplate compiledTemplate = factory.Engine.CompileTemplate (pt, content, host, settings);

			if (settings.CachedTemplates) {
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
