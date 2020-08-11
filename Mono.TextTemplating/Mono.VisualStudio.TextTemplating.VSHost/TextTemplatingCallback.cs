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
using System.Text;
using Mono.TextTemplating;

namespace Mono.VisualStudio.TextTemplating.VSHost
{
	[Serializable]
	public class TextTemplatingCallback
		: ITextTemplatingCallback
	{
		bool isFromOutputDirective;
		readonly ITextTemplatingEngineHost engineHost;

		public TextTemplatingCallback()
		{
			Errors = new TemplateErrorCollection ();
			CodePage = Encoding.UTF8.CodePage;
		}

		public TextTemplatingCallback (ITextTemplatingEngineHost engineHost)
			: this()
		{
			this.engineHost = engineHost ?? throw new ArgumentNullException(nameof(engineHost));
		}

		public TemplateErrorCollection Errors { get; }

		public string Extension { get; private set; } = null;

		public int CodePage { get; private set; }

		public string TemplateOutput { get; private set; }

		public void Initialize()
		{
			Extension = null;
			isFromOutputDirective = false;

			Errors.Clear ();
		}

		public void ErrorCallback (bool warning, string message, int line, int column)
		{
			Errors.Add (new TemplateError (message, new Location (engineHost.TemplateFile, line, column)) {
				IsWarning = warning
			});
		}

		public void SetFileExtension (string extension)
		{
			Extension = extension ?? throw new ArgumentNullException (nameof (extension));
		}

		public void SetOutputEncoding (Encoding encoding, bool fromOutputDirective)
		{
			if (!isFromOutputDirective) {
				if (fromOutputDirective) {
					isFromOutputDirective = true;
					CodePage = encoding?.CodePage ?? throw new ArgumentNullException (nameof (encoding));
				} else {
					CodePage = encoding?.CodePage ?? Encoding.UTF8.CodePage;
				}
			}
		}

		public void SetTemplateOutput (string output)
		{
			TemplateOutput = output;
		}

		public ITextTemplatingCallback DeepCopy ()
		{
			TextTemplatingCallback callback = (TextTemplatingCallback)base.MemberwiseClone ();

			if (Extension != null) {
				callback.Extension = (string)Extension.Clone ();
			}

			if (TemplateOutput != null) {
				callback.TemplateOutput = (string)TemplateOutput.Clone ();
			}

			callback.CodePage = CodePage;

			return callback;
		}

		public Encoding GetOutputEncoding ()
		{
			if (CodePage > 0) {
				return Encoding.GetEncoding (CodePage);
			}
			return Encoding.UTF8;
		}
	}
}
