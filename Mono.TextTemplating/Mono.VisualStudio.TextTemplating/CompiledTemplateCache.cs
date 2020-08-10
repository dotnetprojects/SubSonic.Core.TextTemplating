// 
// CompiledTemplateCache.cs
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
using Mono.TextTemplating;

namespace Mono.VisualStudio.TextTemplating
{
	public static class CompiledTemplateCache
	{
		static readonly Dictionary<string, CompiledTemplateRecord> compiledTemplates = new Dictionary<string, CompiledTemplateRecord> (0x23);
		static DateTime lastUse;

		public static CompiledTemplate Find(string fullClassName)
		{
			CompiledTemplate compiledTemplate = null;
			Dictionary<string, CompiledTemplateRecord> compiledTemplates = CompiledTemplateCache.compiledTemplates;
			lock (compiledTemplates) {
				lastUse = DateTime.Now;
				if (CompiledTemplateCache.compiledTemplates.TryGetValue(fullClassName, out CompiledTemplateRecord record)) {
					compiledTemplate = record.CompiledTemplate;
					record.LastUse = lastUse;
				}
			}
			return compiledTemplate;
		}

		public static void Insert (string classFullName, CompiledTemplate compiledTemplate)
		{
			Dictionary<string, CompiledTemplateRecord> assemblies = CompiledTemplateCache.compiledTemplates;
			lock (assemblies) {
				CompiledTemplateCache.compiledTemplates[classFullName] = new CompiledTemplateRecord (compiledTemplate);
				lastUse = DateTime.Now;
			}
		}
	}
}
