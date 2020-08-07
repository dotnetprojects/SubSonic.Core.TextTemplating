using System;
#if !NET35
using System.Collections.Concurrent;
#endif
using System.Globalization;
using Mono.TextTemplating;

namespace Mono.VisualStudio.TextTemplating.VSHost
{
	[Serializable]
	public class TransformationRunFactory
		: IProcessTransformationRunFactory
	{
#if !NET35
		[NonSerialized]
		readonly ConcurrentDictionary<Guid, IProcessTransformationRunner> runners = new ConcurrentDictionary<Guid, IProcessTransformationRunner> ();
#endif
		[NonSerialized]
		readonly IProcessTextTemplatingEngine engine;


		public const string TransformationRunFactoryService = "TransformationRunFactoryService";
		public const string TransformationRunFactoryMethod = nameof (TransformationRunFactory);

#pragma warning disable CA1051 // Do not declare visible instance fields
		protected readonly Guid id;
#pragma warning restore CA1051 // Do not declare visible instance fields
		
		public TransformationRunFactory (Guid id)
		{
			this.id = id;

			engine = new TemplatingEngine ();
		}


		public IProcessTextTemplatingEngine Engine { get => engine; }

		/// <summary>
		/// get the status of this instance.
		/// </summary>
		public bool IsAlive { get; set; }
		/// <summary>
		/// Create the transformation runner
		/// </summary>
		/// <param name="runnerType"></param>
		/// <returns></returns>
		public IProcessTransformationRunner CreateTransformationRunner (Type runnerType)
		{
			var runnerId = Guid.NewGuid ();
#if !NET35
			if (Activator.CreateInstance (runnerType, new object[] { this, runnerId }) is IProcessTransformationRunner runner) {
				if (runners.TryAdd (runnerId, runner)) {
					return runner;
				}
			}
			return default;
#else
			throw new PlatformNotSupportedException ();
#endif
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="runner"></param>
		/// <returns></returns>
		public string PerformTransformation (Guid runnerId)
		{
#if !NET35
			if (runners.TryGetValue(runnerId, out IProcessTransformationRunner runner)) {
				return runner.PerformTransformation ();
			}
			throw new InvalidOperationException (string.Format (CultureInfo.CurrentCulture, VsTemplatingErrorResources.TransformationRunnerDoesNotExists, runnerId, nameof (CreateTransformationRunner)));
#else
			throw new PlatformNotSupportedException ();
#endif
		}
		/// <summary>
		/// We have no further need for the runner.
		/// </summary>
		/// <param name="runnerId">the runner id</param>
		/// <returns>true, if successful</returns>
		public bool DisposeOfRunner(Guid runnerId)
		{
#if !NET35
			return runners.TryRemove (runnerId, out _);
#else
			throw new PlatformNotSupportedException ();
#endif
		}
	}
}
