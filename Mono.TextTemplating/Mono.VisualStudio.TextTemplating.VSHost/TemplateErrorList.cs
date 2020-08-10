// 
// TemplateErrorList.cs
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
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Mono.VisualStudio.TextTemplating.VSHost
{
	[Serializable]
	public class TemplateErrorCollection
		: IEnumerable<TemplateError>
	{
		readonly ArrayList errors;

		public TemplateErrorCollection()
		{
			errors = new ArrayList ();
		}

		public bool HasErrors => this.Any ();

		public int Count => errors.Count;

		public void Add (TemplateError error) => errors.Add (error);

		public void Clear () => errors.Clear();

		public CompilerErrorCollection ToCompilerErrorCollection()
		{
			return new CompilerErrorCollection (this.Select (x => x.ToCompilerError ()).ToArray ());
		}

		public IEnumerator<TemplateError> GetEnumerator ()
		{
			foreach(TemplateError error in errors) {
				yield return error;
			}
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return errors.GetEnumerator ();
		}
	}
}
