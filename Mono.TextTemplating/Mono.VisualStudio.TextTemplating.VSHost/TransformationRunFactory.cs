using System;
#if !NET35
using System.Collections.Concurrent;
#endif
using System.Globalization;
using Mono.TextTemplating;

namespace Mono.VisualStudio.TextTemplating.VSHost
{
	[Serializable]
	public abstract class TransformationRunFactory
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
			this.id = id;

			engine = new TemplatingEngine ();
		}

		public Guid ID { get => id; }

		public IProcessTextTemplatingEngine Engine { get => engine; }

		public Guid GetFactoryId () => id;
		/// <summary>
		/// get the status of this instance.
		/// </summary>
		public bool IsRunFactoryAlive () => !id.Equals (Guid.Empty);



		/// <summary>
		/// Create the transformation runner
		/// </summary>
		/// <returns>instanciated transformation runner</returns>
		public abstract IProcessTransformationRunner CreateTransformationRunner ();

		public virtual bool PrepareTransformation (Guid runnerId, ParsedTemplate pt, string content, ITextTemplatingEngineHost host, TemplateSettings settings)
		{
#if !NET35
			if (Runners.TryGetValue(runnerId, out IProcessTransformationRunner _runner) &&
				_runner is TransformationRunner runner) {
				return runner.PrepareTransformation (pt, content, host, settings);
			}
			return default;
#else
			throw new NotSupportedException ();
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
			if (Runners.TryGetValue(runnerId, out IProcessTransformationRunner _runner) &&
				_runner is TransformationRunner runner) {
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
