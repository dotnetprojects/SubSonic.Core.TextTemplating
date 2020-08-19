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
using System.Reflection;

namespace Mono.TextTemplating
{
	internal static class StringUtil
	{
		public static bool IsNullOrWhiteSpace (String value)
		{
			if (value == null) return true;

			for (int i = 0; i < value.Length; i++) {
				if (!char.IsWhiteSpace (value[i])) return false;
			}

			return true;
		}

		public static TType GetValue<TType>(this PropertyInfo property, object @object, object[] index)
		{
			if (property.GetValue(@object, index) is TType success) {
				return success;
			}
			return default;
		}


		public static bool TryParse<TEnum> (this string value, out TEnum @enum)
			where TEnum: struct
		{
#if NET35
			if (Enum.Parse (typeof (TEnum), value) is TEnum success) {
				@enum = success;

				return true;
			}

			@enum = default;

			return false;
#else
			return Enum.TryParse (value, out @enum);
#endif
		}

	}
}
