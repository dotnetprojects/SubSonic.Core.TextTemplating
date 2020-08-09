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
		public readonly static ConcurrentDictionary<Guid, IProcessTransformationRunner> Runners = new ConcurrentDictionary<Guid, IProcessTransformationRunner> ();
#endif
		[NonSerialized]
		readonly IProcessTextTemplatingEngine engine;


		public const string TransformationRunFactoryService = "TransformationRunFactoryService";
		public const string TransformationRunFactoryMethod = nameof (TransformationRunFactory);

		readonly Guid id;

		public TransformationRunFactory (Guid id)
		{
			FactoryId = id;

			engine = new TemplatingEngine ();
		}

		public Guid ID { get => id; }

		public IProcessTextTemplatingEngine Engine { get => engine; }

		/// <summary>
		/// get the status of this instance.
		/// </summary>
		public bool IsAlive { get => !FactoryId.Equals (Guid.Empty); }

		public Guid FactoryId { get; private set; }

		/// <summary>
		/// Create the transformation runner
		/// </summary>
		/// <param name="runnerType"></param>
		/// <returns></returns>
		public virtual IProcessTransformationRunner CreateTransformationRunner (Type runnerType)
		{
			var runnerId = Guid.NewGuid ();
#if !NET35
			if (Activator.CreateInstance (runnerType, new object[] { this, runnerId }) is IProcessTransformationRunner runner) {
				if (Runners.TryAdd (runnerId, runner)) {
					return runner;
				}
			}
			return default;
#else
			throw new PlatformNotSupportedException ();
#endif
		}

		public virtual bool PrepareTransformation (Guid runnerId, ParsedTemplate pt, string content, ITextTemplatingEngineHost host, TemplateSettings settings)
		{
#if !NET35
			if (Runners.TryGetValue(runnerId, out IProcessTransformationRunner runner)) {
				return runner.PrepareTransformation (pt, content, host, settings);
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
		public string StartTransformation (Guid runnerId)
		{
#if !NET35
			if (Runners.TryGetValue(runnerId, out IProcessTransformationRunner runner)) {
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
			return Runners.TryRemove (runnerId, out _);
#else
			throw new PlatformNotSupportedException ();
#endif
		}
	}
}
