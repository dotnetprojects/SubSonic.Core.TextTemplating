// 
// ProcessTextTemplateEngine.cs
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
using Mono.VisualStudio.TextTemplating;
using Mono.VisualStudio.TextTemplating.VSHost;

namespace Mono.TextTemplating
{
	using System.Globalization;
	using System.Runtime.Serialization;
	using System.Threading;

	public partial class TemplatingEngine
		: IProcessTextTemplatingEngine
	{
		public IProcessTransformationRunner PrepareTransformationRunner (string content, ITextTemplatingEngineHost host, IProcessTransformationRunFactory runFactory, bool debugging = false)
		{
			if (content == null) {
				throw new ArgumentNullException (nameof (content));
			}
			if (host == null) {
				throw new ArgumentNullException (nameof (host));
			}
			if (runFactory == null) {
				throw new ArgumentNullException (nameof (runFactory));
			}

			if (host is ITextTemplatingSessionHost sessionHost) {
				if (sessionHost.Session == null) {
					sessionHost.Session = sessionHost.CreateSession ();
				}
			}

			ParsedTemplate pt = ParsedTemplate.FromText (content, host);

			IProcessTransformationRunner run = null;

			try {
				if (pt.Errors.HasErrors) {
					return null;
				}
				TemplateSettings settings = GetSettings (host, pt);

				settings.Debug = debugging;

				run = CompileAndPrepareRun (pt, content, host, runFactory, settings);
			}
			catch (Exception ex) {
				if (IsCriticalException (ex)) {
					throw;
				}
				pt.LogError (string.Format (CultureInfo.CurrentCulture, VsTemplatingErrorResources.ExceptionProcessingTemplate, ex), new Location (host.TemplateFile, -1, -1));
			}
			finally {
				host.LogErrors (pt.Errors.ToCompilerErrorCollection());
			}

			return run;
		}

		protected virtual IProcessTransformationRunner CompileAndPrepareRun (ParsedTemplate pt, string content, ITextTemplatingEngineHost host, IProcessTransformationRunFactory runFactory, TemplateSettings settings)
		{
			TransformationRunner runner = null;
			bool success = false;

			if (pt == null) {
				throw new ArgumentNullException (nameof (pt));
			}

			if (host == null) {
				throw new ArgumentNullException (nameof (host));
			}

			if (runFactory == null) {
				throw new ArgumentNullException (nameof (runFactory));
			}

			if (settings == null) {
				throw new ArgumentNullException (nameof (settings));
			}

			try {
				try {
					if (runFactory.CreateTransformationRunner () is TransformationRunner theRunner) {
						runner = theRunner;
					}
				}
				catch (Exception ex) {
					if (IsCriticalException (ex)) {
						throw;
					}
					pt.LogError (ex.ToString (), new Location (host.TemplateFile));
				}
				if (runner != null && !runner.Errors.HasErrors) {
					ProcessReferences (host, pt, settings);
					if (!pt.Errors.HasErrors) {
						try {
							success = runFactory.PrepareTransformation (runner.RunnerId, pt, content, settings.HostSpecific ? host : null, settings);
						}
						catch (SerializationException) {
							pt.LogError (VsTemplatingErrorResources.SessionHostMarshalError, new Location (host.TemplateFile));
							throw;
						}
					}
				}
			}
			catch (Exception ex) {
				if (IsCriticalException (ex)) {
					throw;
				}
				pt.LogError (ex.ToString (), new Location (host.TemplateFile, -1, -1));
			}
			//finally {
			//	if (runner != null) {
			//		// using RPC this will not be possible
			//		pt.Errors.AddRange (runner.Errors.ToCompilerErrorCollection());
			//		runner.ClearErrors ();
			//	}
			//}

			return success ? runner : null;
		}

		public static bool IsCriticalException (Exception e)
		{
			return ((e is StackOverflowException) || ((e is OutOfMemoryException) || ((e is ThreadAbortException) || ((e.InnerException != null) && IsCriticalException (e.InnerException)))));
		}
	}
}
