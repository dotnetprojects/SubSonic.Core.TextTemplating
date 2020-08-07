using System;
using Mono.TextTemplating;

namespace Mono.VisualStudio.TextTemplating.VSHost
{ 
	[Serializable]
	public abstract class TransformationRunFactory
		: IProcessTransformationRunFactory
	{
		public const string TransformationRunFactoryService = "TransformationRunFactoryService";
		public const string TransformationRunFactoryMethod = nameof(TransformationRunFactory);

#pragma warning disable CA1051 // Do not declare visible instance fields
		protected readonly Guid id;
#pragma warning restore CA1051 // Do not declare visible instance fields

		protected TransformationRunFactory (Guid id)
		{
			this.id = id;
		}

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
			if (Activator.CreateInstance(runnerType, new object[] { id }) is IProcessTransformationRunner runner) {
				return runner;
			}
			return default;
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="runner"></param>
		/// <returns></returns>
		public abstract string StartTransformation (IProcessTransformationRunner runner);
	}
}
