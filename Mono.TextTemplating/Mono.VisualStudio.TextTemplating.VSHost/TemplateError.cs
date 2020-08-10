// 
// TemplateError.cs
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
using System.Collections.Generic;
using System.Text;
using Mono.TextTemplating;

namespace Mono.VisualStudio.TextTemplating.VSHost
{
	[Serializable]
	public class TemplateError
	{
		public TemplateError () { }

		public TemplateError(string message, Location location)
			: this(message, string.Empty, location, false)
		{ }

		public TemplateError (string message, string errorNumber, Location location)
			: this (message, errorNumber, location, false)
		{ }

		public TemplateError (string message, string errorNumber, Location location, bool isWarning)
		{
			Message = message;
			ErrorNumber = errorNumber;
			Location = location;
			IsWarning = isWarning;
		}

		public string ErrorNumber { get; set; }
		public string Message { get; set; }
		public Location Location { get; set; }
		public bool IsWarning { get; set; }

		public CompilerError ToCompilerError()
		{
			return new CompilerError (Location.FileName, Location.Line, Location.Column, ErrorNumber, Message) {
				IsWarning = IsWarning
			};
		}
	}
}
