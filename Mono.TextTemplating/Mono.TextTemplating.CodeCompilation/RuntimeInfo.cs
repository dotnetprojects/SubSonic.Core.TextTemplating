//
// FrameworkHelpers.cs
//
// Author:
//       Mikayla Hutchinson <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2018 Microsoft Corp
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
using System.IO;
using System.Linq;

namespace Mono.TextTemplating.CodeCompilation
{
	public enum RuntimeKind
	{
		Default = 0,
		NetCore,
		AspNetCore,
		NetFramework,
		Mono
	}

	class RuntimeInfo
	{
		RuntimeInfo (RuntimeKind kind) => Kind = kind;

		static RuntimeInfo FromError (RuntimeKind kind, string error) => new RuntimeInfo (kind) { Error = error };

		public RuntimeKind Kind { get; private set; }
		public string Error { get; private set; }
		public string RuntimeDir { get; private set; }
		public string CscPath { get; private set; }
		public bool IsValid => Error == null;

		public static RuntimeInfo GetRuntime (RuntimeKind kind = RuntimeKind.Default)
		{
			var monoFx = GetMonoRuntime ();
			if (monoFx.IsValid && (monoFx.Kind == kind || kind == RuntimeKind.Default)) {
				return monoFx;
			}
			var netFx = GetNetFrameworkRuntime ();
			if (netFx.IsValid && (netFx.Kind == kind || kind == RuntimeKind.Default)) {
				return netFx;
			}
			var coreFx = GetDotNetCoreRuntime ();
			if (coreFx.IsValid && (coreFx.Kind == kind || kind == RuntimeKind.Default)) {
				return coreFx;
			}
			var aspCoreFx = GetAspDotNetCoreRuntime ();
			if (aspCoreFx.IsValid && (aspCoreFx.Kind == kind || kind == RuntimeKind.Default)) {
				return aspCoreFx;
			}
			return FromError (RuntimeKind.Mono, "Could not find any valid runtime" );
		}

		public static RuntimeInfo GetMonoRuntime ()
		{
			if (Type.GetType ("Mono.Runtime") == null) {
				return FromError (RuntimeKind.Mono, "Current runtime is not Mono" );
			}

			var runtimeDir = Path.GetDirectoryName (typeof (int).Assembly.Location);
			var csc = Path.Combine (runtimeDir, "csc.exe");
			if (!File.Exists (csc)) {
				return FromError (RuntimeKind.Mono, "Could not find csc in host Mono installation" );
			}

			return new RuntimeInfo (RuntimeKind.Mono) {
				CscPath = csc,
				RuntimeDir = runtimeDir
			};
		}

		public static RuntimeInfo GetNetFrameworkRuntime ()
		{
			var runtimeDir = Path.GetDirectoryName (typeof (int).Assembly.Location);
			var csc = Path.Combine (runtimeDir, "csc.exe");
			if (!File.Exists (csc)) {
				return FromError (RuntimeKind.NetFramework, "Could not find csc in host .NET Framework installation");
			}
			return new RuntimeInfo (RuntimeKind.NetFramework) {
				CscPath = csc,
				RuntimeDir = runtimeDir
			};
		}

		public static RuntimeInfo GetDotNetCoreRuntime ()
		{
			var dotnetRoot = FindDotNetRoot ();
			if (dotnetRoot == null) {
				return FromError (RuntimeKind.NetCore, "Could not find .NET Core installation");
			}

			string MakeCscPath (string d) => Path.Combine (d, "Roslyn", "bincore", "csc.dll");

			var version = System.Environment.Version.ToString ();

			var runtimeDir = Path.Combine (dotnetRoot, "shared", "Microsoft.NETCore.App", version);
			if (!File.Exists (Path.Combine (runtimeDir, "System.Runtime.dll"))) {
				runtimeDir = FindHighestVersionedDirectory (Path.Combine (dotnetRoot, "shared", "Microsoft.NETCore.App"), d => File.Exists (Path.Combine (d, "System.Runtime.dll")));
				if (runtimeDir == null) {
					return FromError (RuntimeKind.NetCore, "Could not find System.Runtime.dll in any .NET shared runtime");
				}
			}

			var sdkDir = FindHighestVersionedDirectory (Path.Combine (dotnetRoot, "sdk"), d => File.Exists (MakeCscPath (d)), version);
			if (sdkDir == null)
				sdkDir = FindHighestVersionedDirectory (Path.Combine (dotnetRoot, "sdk"), d => File.Exists (MakeCscPath (d)), string.Join (".", version.Split ('.').Take (2)));
			if (sdkDir == null)
				sdkDir = FindHighestVersionedDirectory (Path.Combine (dotnetRoot, "sdk"), d => File.Exists (MakeCscPath (d)), version.Split ('.')[0]);
			if (sdkDir == null)
				sdkDir = FindHighestVersionedDirectory (Path.Combine (dotnetRoot, "sdk"), d => File.Exists (MakeCscPath (d)));
			if (sdkDir == null) {
				return FromError (RuntimeKind.NetCore, "Could not find csc.dll in any .NET Core SDK");
			}

			return new RuntimeInfo (RuntimeKind.NetCore) { RuntimeDir = runtimeDir, CscPath = MakeCscPath (sdkDir) };
		}

		public static RuntimeInfo GetAspDotNetCoreRuntime ()
		{
			var dotnetRoot = FindDotNetRoot ();
			if (dotnetRoot == null) {
				return FromError (RuntimeKind.AspNetCore, "Could not find .NET Core installation");
			}

			string MakeCscPath (string d) => Path.Combine (d, "Roslyn", "bincore", "csc.dll");

			var version = System.Environment.Version.ToString ();

			var runtimeDir = Path.Combine (dotnetRoot, "shared", "Microsoft.AspNetCore.App", version);
			if (!File.Exists (Path.Combine (runtimeDir, "System.Runtime.dll"))) {
				runtimeDir = FindHighestVersionedDirectory (Path.Combine (dotnetRoot, "shared", "Microsoft.AspNetCore.App"), d => File.Exists (Path.Combine (d, "System.Runtime.dll")));
				if (runtimeDir == null) {
					return FromError (RuntimeKind.AspNetCore, "Could not find System.Runtime.dll in any .NET shared runtime");
				}
			}

			var sdkDir = FindHighestVersionedDirectory (Path.Combine (dotnetRoot, "sdk"), d => File.Exists (MakeCscPath (d)), version);
			if (sdkDir == null)
				sdkDir = FindHighestVersionedDirectory (Path.Combine (dotnetRoot, "sdk"), d => File.Exists (MakeCscPath (d)), string.Join (".", version.Split ('.').Take (2)));
			if (sdkDir == null)
				sdkDir = FindHighestVersionedDirectory (Path.Combine (dotnetRoot, "sdk"), d => File.Exists (MakeCscPath (d)), version.Split ('.')[0]);
			if (sdkDir == null)
				sdkDir = FindHighestVersionedDirectory (Path.Combine (dotnetRoot, "sdk"), d => File.Exists (MakeCscPath (d)));
			if (sdkDir == null) {
				return FromError (RuntimeKind.AspNetCore, "Could not find csc.dll in any .NET Core SDK");
			}

			return new RuntimeInfo (RuntimeKind.AspNetCore) { RuntimeDir = runtimeDir, CscPath = MakeCscPath (sdkDir) };
		}

		static string FindDotNetRoot ()
		{
			string dotnetRoot;
			bool DotnetRootIsValid () => !string.IsNullOrEmpty (dotnetRoot) && (File.Exists (Path.Combine (dotnetRoot, "dotnet")) || File.Exists (Path.Combine (dotnetRoot, "dotnet.exe")));

			string FindInPath (string name) => (Environment.GetEnvironmentVariable ("PATH") ?? "")
				.Split (new [] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
				.Select (p => Path.Combine (p, name))
				.FirstOrDefault (File.Exists);

			dotnetRoot = Environment.GetEnvironmentVariable ("DOTNET_ROOT");
			if (DotnetRootIsValid ()) {
				return dotnetRoot;
			}

			// this should get us something like /usr/local/share/dotnet/shared/Microsoft.NETCore.App/2.1.2/System.Runtime.dll
			var runtimeDir = Path.GetDirectoryName (typeof (int).Assembly.Location);
			dotnetRoot = Path.GetDirectoryName (Path.GetDirectoryName (Path.GetDirectoryName (runtimeDir)));

			if (DotnetRootIsValid ()) {
				return dotnetRoot;
			}

			dotnetRoot = Path.GetDirectoryName (FindInPath (Path.DirectorySeparatorChar == '\\' ? "dotnet.exe" : "dotnet"));
			if (DotnetRootIsValid ()) {
				return dotnetRoot;
			}

			return null;
		}

		static string FindHighestVersionedDirectory (string parentFolder, Func<string, bool> validate, string startingWith = null)
		{
			string bestMatch = null;
			var bestVersion = SemVersion.Zero;
			foreach (var dir in Directory.EnumerateDirectories (parentFolder)) {
				var name = Path.GetFileName (dir);
				if (startingWith == null || name.StartsWith (startingWith)) {
					if (!name.Contains ("-")) {
						if (SemVersion.TryParse (name, out var version) && version.Major >= 0) {
							if (version > bestVersion && (validate == null || validate (dir))) {
								bestVersion = version;
								bestMatch = dir;
							}
						}
					}
				}
			}
			foreach (var dir in Directory.EnumerateDirectories (parentFolder)) {
				var name = Path.GetFileName (dir);
				if (startingWith == null || name.StartsWith (startingWith)) {
					if (name.Contains ("-")) {
						name = name.Split ('-')[0];
						if (SemVersion.TryParse (name, out var version) && version.Major >= 0) {
							if (version > bestVersion && (validate == null || validate (dir))) {
								bestVersion = version;
								bestMatch = dir;
							}
						}
					}
				}
			}
			return bestMatch;
		}

		
	}
}
