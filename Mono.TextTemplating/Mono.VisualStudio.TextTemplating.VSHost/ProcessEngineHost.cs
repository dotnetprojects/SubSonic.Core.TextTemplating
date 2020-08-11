// 
// TextTemplatingCallback.cs
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
using System.Collections.Generic;
using System.Text;
using Mono.TextTemplating;

namespace Mono.VisualStudio.TextTemplating.VSHost
{
	[Serializable]
	public abstract class ProcessEngineHost
		: ITextTemplatingEngineHost
		, ITextTemplatingSessionHost
	{
		public ProcessEngineHost()
		{
			StandardAssemblyReferences = new List<string> ();
			StandardImports = new List<string> ();
			Callback = new TextTemplatingCallback(this);
		}

		public ITextTemplatingCallback Callback { get; }

		public IList<string> StandardAssemblyReferences { get; }

		public IList<string> StandardImports { get; }

		public string TemplateFile { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
		public ITextTemplatingSession Session { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

		public virtual ITextTemplatingSession CreateSession ()
		{
			return new TextTemplatingSession ();
		}

#if FEATURE_APPDOMAINS
		public abstract AppDomain ProvideTemplatingAppDomain (string context);
#endif

		public abstract object GetHostOption (string optionName);

		public abstract bool LoadIncludeText (string requestFileName, out string content, out string location);

		public void LogErrors (TemplateErrorCollection errors)
		{
			Callback.Errors.AddRange (errors);
		}

		public abstract string ResolveAssemblyReference (string assemblyReference);

		public abstract Type ResolveDirectiveProcessor (string processorName);

		public abstract string ResolveParameterValue (string directiveId, string processorName, string parameterName);

		public abstract string ResolvePath (string path);

		public void SetFileExtension (string extension)
		{
			Callback.SetFileExtension (extension);
		}

		public void SetOutputEncoding (Encoding encoding, bool fromOutputDirective)
		{
			Callback.SetOutputEncoding (encoding, fromOutputDirective);
		}

		public void SetTemplateOutput(string templateOutput)
		{
			Callback.SetTemplateOutput (templateOutput);
		}
	}
}
