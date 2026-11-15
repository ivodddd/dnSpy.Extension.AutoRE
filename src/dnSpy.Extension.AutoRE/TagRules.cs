/*
    Copyright (C) 2014-2019 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;

namespace dnSpy.AutoRE.Analysis {
	/// <summary>
	/// Maps type names, member names and P/Invoke imports to <see cref="ApiTag"/>s
	/// </summary>
	static class TagRules {
		static readonly string[] antiDebugMembers = new string[] {
			"IsDebuggerPresent",
			"CheckRemoteDebuggerPresent",
			"NtQueryInformationProcess",
			"NtSetInformationThread",
			"NtQuerySystemInformation",
			"NtQueryObject",
			"DebugActiveProcess",
			"OutputDebugString",
			"BlockInput",
			"QueryPerformanceCounter",
			"GetTickCount",
		};

		public static ApiTag GetTag(string? typeName, string? memberName) {
			if (string.IsNullOrEmpty(typeName))
				return ApiTag.None;
			var tags = GetTypeTag(typeName!);
			if (!string.IsNullOrEmpty(memberName))
				tags |= GetMemberTag(typeName!, memberName!);
			return tags;
		}

		static ApiTag GetTypeTag(string typeName) {
			switch (typeName) {
			case "System.Diagnostics.Debugger":
				return ApiTag.Debug;
			case "System.Diagnostics.Debug":
				return ApiTag.Debug;
			case "System.Diagnostics.Process":
			case "System.Diagnostics.ProcessStartInfo":
			case "System.Diagnostics.ProcessModule":
			case "System.Diagnostics.ProcessThread":
				return ApiTag.Process;
			case "System.Environment":
				return ApiTag.Environment;
			case "System.Runtime.InteropServices.Marshal":
			case "System.Runtime.InteropServices.MemoryMarshal":
			case "System.Runtime.InteropServices.SafeHandle":
			case "System.Runtime.CompilerServices.Unsafe":
				return ApiTag.Memory | ApiTag.Native;
			}

			if (typeName.StartsWith("System.IO", StringComparison.Ordinal))
				return ApiTag.File;
			if (typeName.StartsWith("Microsoft.Win32.Registry", StringComparison.Ordinal))
				return ApiTag.Registry;
			if (typeName.StartsWith("Microsoft.Win32.SafeHandles", StringComparison.Ordinal))
				return ApiTag.Native;
			if (typeName.StartsWith("System.Net", StringComparison.Ordinal))
				return ApiTag.Network;
			if (typeName.StartsWith("System.Security.Cryptography", StringComparison.Ordinal))
				return ApiTag.Crypto;
			if (typeName.StartsWith("System.Diagnostics", StringComparison.Ordinal))
				return ApiTag.Process;
			if (typeName.StartsWith("System.Threading", StringComparison.Ordinal))
				return ApiTag.Threading;
			if (typeName.StartsWith("System.Runtime.InteropServices", StringComparison.Ordinal))
				return ApiTag.Native;
			if (typeName.StartsWith("System.Reflection", StringComparison.Ordinal))
				return ApiTag.Reflection;
			if (typeName.StartsWith("System.Runtime.Serialization", StringComparison.Ordinal) ||
				typeName.StartsWith("System.Xml.Serialization", StringComparison.Ordinal) ||
				typeName.StartsWith("System.Text.Json", StringComparison.Ordinal) ||
				typeName.StartsWith("Newtonsoft.Json", StringComparison.Ordinal))
				return ApiTag.Serialization;
			if (typeName.StartsWith("System.Windows", StringComparison.Ordinal) ||
				typeName.StartsWith("System.Drawing", StringComparison.Ordinal) ||
				typeName.StartsWith("System.Media", StringComparison.Ordinal))
				return ApiTag.Ui;
			if (typeName.StartsWith("System.Security", StringComparison.Ordinal))
				return ApiTag.Security;
			if (typeName.StartsWith("System.Text", StringComparison.Ordinal))
				return ApiTag.Text;
			if (typeName.StartsWith("System.Management", StringComparison.Ordinal))
				return ApiTag.Process;
			return ApiTag.None;
		}

		static ApiTag GetMemberTag(string typeName, string memberName) {
			if (typeName == "System.Diagnostics.Debugger" &&
				(memberName == "IsAttached" || memberName == "IsLogging" || memberName == "Break" || memberName == "Launch"))
				return ApiTag.AntiDebug;
			if (IsAntiDebugName(memberName))
				return ApiTag.AntiDebug;
			if (typeName == "System.Diagnostics.Process" && memberName == "Start")
				return ApiTag.Process;
			return ApiTag.None;
		}

		/// <summary>
		/// Gets tags from a string literal, eg. URLs, registry paths, certificates
		/// </summary>
		public static ApiTag GetStringTag(string? value) {
			if (string.IsNullOrEmpty(value))
				return ApiTag.None;
			var tags = ApiTag.None;
			if (value!.IndexOf("http://", StringComparison.OrdinalIgnoreCase) >= 0 ||
				value.IndexOf("https://", StringComparison.OrdinalIgnoreCase) >= 0 ||
				value.IndexOf("ftp://", StringComparison.OrdinalIgnoreCase) >= 0)
				tags |= ApiTag.Network;
			if (value.IndexOf("HKEY_", StringComparison.OrdinalIgnoreCase) >= 0 ||
				value.IndexOf("SOFTWARE\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
				value.IndexOf("CurrentVersion\\Run", StringComparison.OrdinalIgnoreCase) >= 0)
				tags |= ApiTag.Registry;
			if (value.IndexOf("%APPDATA%", StringComparison.OrdinalIgnoreCase) >= 0 ||
				value.IndexOf("%TEMP%", StringComparison.OrdinalIgnoreCase) >= 0 ||
				value.IndexOf("%PROGRAMDATA%", StringComparison.OrdinalIgnoreCase) >= 0)
				tags |= ApiTag.File;
			if (value.IndexOf("BEGIN RSA PRIVATE KEY", StringComparison.OrdinalIgnoreCase) >= 0 ||
				value.IndexOf("BEGIN CERTIFICATE", StringComparison.OrdinalIgnoreCase) >= 0 ||
				value.IndexOf("BEGIN PRIVATE KEY", StringComparison.OrdinalIgnoreCase) >= 0)
				tags |= ApiTag.Crypto;
			return tags;
		}

		public static bool IsAntiDebugName(string name) {
			foreach (var s in antiDebugMembers) {
				if (name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
					return true;
			}
			return false;
		}

		/// <summary>
		/// Gets the tag of a P/Invoke method
		/// </summary>
		/// <param name="moduleName">Native module, eg. kernel32</param>
		/// <param name="entryPoint">Native entry point, eg. CreateFileW</param>
		public static ApiTag GetPInvokeTag(string? moduleName, string? entryPoint) {
			var tags = ApiTag.Native;
			var module = (moduleName ?? string.Empty).ToLowerInvariant();
			var name = (entryPoint ?? string.Empty).ToLowerInvariant();

			if (module.IndexOf("advapi", StringComparison.Ordinal) >= 0)
				tags |= ApiTag.Registry | ApiTag.Security;
			if (module.IndexOf("crypt", StringComparison.Ordinal) >= 0 ||
				module.IndexOf("bcrypt", StringComparison.Ordinal) >= 0 ||
				module.IndexOf("ncrypt", StringComparison.Ordinal) >= 0)
				tags |= ApiTag.Crypto;
			if (module.IndexOf("ws2", StringComparison.Ordinal) >= 0 ||
				module.IndexOf("wininet", StringComparison.Ordinal) >= 0 ||
				module.IndexOf("winhttp", StringComparison.Ordinal) >= 0 ||
				module.IndexOf("urlmon", StringComparison.Ordinal) >= 0 ||
				module.IndexOf("dnsapi", StringComparison.Ordinal) >= 0)
				tags |= ApiTag.Network;
			if (module.IndexOf("user32", StringComparison.Ordinal) >= 0 ||
				module.IndexOf("gdi32", StringComparison.Ordinal) >= 0 ||
				module.IndexOf("comdlg32", StringComparison.Ordinal) >= 0 ||
				module.IndexOf("comctl32", StringComparison.Ordinal) >= 0)
				tags |= ApiTag.Ui;
			if (module.IndexOf("shell32", StringComparison.Ordinal) >= 0)
				tags |= ApiTag.File;
			if (module.IndexOf("psapi", StringComparison.Ordinal) >= 0)
				tags |= ApiTag.Process;

			if (name.StartsWith("reg", StringComparison.Ordinal))
				tags |= ApiTag.Registry;
			if (name.IndexOf("file", StringComparison.Ordinal) >= 0 ||
				name.IndexOf("directory", StringComparison.Ordinal) >= 0 ||
				name.StartsWith("findfirst", StringComparison.Ordinal))
				tags |= ApiTag.File;
			if (name.IndexOf("socket", StringComparison.Ordinal) >= 0 ||
				name.StartsWith("wsa", StringComparison.Ordinal) ||
				name.IndexOf("connect", StringComparison.Ordinal) >= 0 ||
				name.StartsWith("send", StringComparison.Ordinal) ||
				name.StartsWith("recv", StringComparison.Ordinal) ||
				name.StartsWith("http", StringComparison.Ordinal) ||
				name.StartsWith("internet", StringComparison.Ordinal))
				tags |= ApiTag.Network;
			if (name.StartsWith("crypt", StringComparison.Ordinal) ||
				name.IndexOf("protectdata", StringComparison.Ordinal) >= 0)
				tags |= ApiTag.Crypto;
			if (name.IndexOf("process", StringComparison.Ordinal) >= 0 ||
				name.IndexOf("shellexecute", StringComparison.Ordinal) >= 0 ||
				name.IndexOf("winexec", StringComparison.Ordinal) >= 0)
				tags |= ApiTag.Process;
			if (name.IndexOf("virtualalloc", StringComparison.Ordinal) >= 0 ||
				name.IndexOf("virtualprotect", StringComparison.Ordinal) >= 0 ||
				name.IndexOf("heapalloc", StringComparison.Ordinal) >= 0 ||
				name.IndexOf("memcpy", StringComparison.Ordinal) >= 0 ||
				name.IndexOf("memmove", StringComparison.Ordinal) >= 0 ||
				name.IndexOf("allocatevirtualmemory", StringComparison.Ordinal) >= 0)
				tags |= ApiTag.Memory;
			if (name.IndexOf("createthread", StringComparison.Ordinal) >= 0)
				tags |= ApiTag.Threading;
			if (name.IndexOf("token", StringComparison.Ordinal) >= 0 ||
				name.IndexOf("privilege", StringComparison.Ordinal) >= 0)
				tags |= ApiTag.Security;
			if (IsAntiDebugName(name))
				tags |= ApiTag.AntiDebug;
			return tags;
		}
	}
}
