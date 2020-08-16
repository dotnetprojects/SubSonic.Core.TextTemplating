// 
// TransformationRunFactory.cs
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
		public ITextTemplatingCallback StartTransformation (Guid runnerId)
		{
#if !NET35
			if (Runners.TryGetValue(runnerId, out IProcessTransformationRunner _runner) &&
				_runner is TransformationRunner runner) {
				return runner.PerformTransformation ();
			}
			throw new InvalidOperationException (string.Format (CultureInfo.CurrentCulture, VsTemplatingErrorResources.TransformationRunnerDoesNotExists, runnerId, nameof (CreateTransformationRunner)));
#else
			throw new NotSupportedException ();
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
			return Runners.TryRemove (runnerId, out IProcessTransformationRunner runner);

			if (runner is IDisposable disposable) {
				disposable.Dispose ();
			}
#else
			throw new NotSupportedException ();
#endif
		}

		public TemplateErrorCollection GetErrors (Guid runnerId)
		{
#if !NET35
			if (Runners.TryGetValue (runnerId, out IProcessTransformationRunner _runner) &&
				_runner is TransformationRunner runner) {
				return runner.Errors;
			}
			throw new InvalidOperationException (string.Format (CultureInfo.CurrentCulture, VsTemplatingErrorResources.TransformationRunnerDoesNotExists, runnerId, nameof (CreateTransformationRunner)));
#else
			throw new NotSupportedException ();
#endif
		}
	}
}
