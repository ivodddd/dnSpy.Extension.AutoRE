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

using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace dnSpy.AutoRE.Analysis {
	/// <summary>
	/// Scans method bodies and produces <see cref="MethodAnalysis"/>s: API tags plus, for
	/// trivial wrappers, a better method name. Inspired by AutoRE
	/// (https://github.com/a1ext/auto_re) by a1ext.
	/// </summary>
	sealed class TagEngine {
		const int MaxApisPerMethod = 12;

		static readonly Regex dummyNameRegex = new Regex(
			@"^(method|Method|sub|Sub|func|Func|m)_?[0-9A-Fa-f]+$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant);

		public void AnalyzeModule(ModuleDef module, AnalysisResult result, CancellationToken cancellationToken) {
			foreach (var type in module.GetTypes()) {
				cancellationToken.ThrowIfCancellationRequested();
				foreach (var method in type.Methods) {
					var analysis = AnalyzeMethod(method);
					if (analysis is not null)
						result.Methods.Add(analysis);
				}
			}
		}

		MethodAnalysis? AnalyzeMethod(MethodDef method) {
			var tags = ApiTag.None;
			var apis = new List<string>();

			if (method.ImplMap is ImplMap implMap) {
				tags |= TagRules.GetPInvokeTag(implMap.Module?.Name, implMap.Name);
				AddApi(apis, $"{implMap.Module?.Name}!{implMap.Name}");
			}

			if (method.HasBody) {
				foreach (var instr in method.Body.Instructions) {
					if (instr.Operand is IMethod target) {
						var typeName = GetTypeName(target.DeclaringType);
						tags |= TagRules.GetTag(typeName, target.Name);
						AddApi(apis, typeName is null ? target.Name : $"{typeName}.{target.Name}");
					}
					else if (instr.Operand is IField field) {
						var typeName = GetTypeName(field.DeclaringType);
						tags |= TagRules.GetTag(typeName, field.Name);
						AddApi(apis, typeName is null ? field.Name : $"{typeName}.{field.Name}");
					}
					else if (instr.Operand is ITypeDefOrRef type) {
						tags |= TagRules.GetTag(GetTypeName(type), null);
					}
					else if (instr.OpCode.Code == Code.Ldstr && instr.Operand is string s) {
						tags |= TagRules.GetStringTag(s);
					}
				}
			}

			var canRename = CanRename(method);
			var proposedName = canRename ? GetProposedName(method) : null;
			if (tags == ApiTag.None && proposedName is null)
				return null;

			return new MethodAnalysis(method, tags, apis, proposedName, canRename);
		}

		static void AddApi(List<string> apis, string api) {
			if (apis.Count >= MaxApisPerMethod)
				return;
			if (!apis.Contains(api))
				apis.Add(api);
		}

		static string? GetTypeName(ITypeDefOrRef? type) {
			if (type is null)
				return null;
			string name;
			try {
				name = type.FullName ?? type.Name ?? string.Empty;
			}
			catch {
				return type.Name;
			}
			// TypeSpecs, eg. List`1<System.String> or System.Nullable`1<System.Int32>
			var index = name.IndexOf('<');
			if (index >= 0)
				name = name.Substring(0, index);
			index = name.IndexOf('`');
			if (index >= 0) {
				// Keep the arity out of the name, eg. List`1 -> List
				var end = index + 1;
				while (end < name.Length && char.IsDigit(name[end]))
					end++;
				name = name.Substring(0, index) + name.Substring(end);
			}
			return name;
		}

		/// <summary>
		/// Returns true if renaming this method can't break anything (virtual dispatch,
		/// property/event accessors, constructors, ...)
		/// </summary>
		static bool CanRename(MethodDef method) {
			if (method.IsConstructor || method.IsStaticConstructor)
				return false;
			if (method.IsVirtual || method.HasOverrides)
				return false;
			if (method.IsSpecialName || method.IsRuntimeSpecialName || method.IsRuntime)
				return false;
			if (method.IsPinvokeImpl || method.IsAbstract)
				return false;
			var type = method.DeclaringType;
			if (type is null || type.IsInterface || type.IsImport)
				return false;
			return true;
		}

		/// <summary>
		/// Returns true if the name looks like an obfuscator generated name
		/// </summary>
		public static bool IsDummyName(string name) {
			if (string.IsNullOrEmpty(name))
				return true;
			if (name.Length == 1)
				return true;
			bool allControlChars = true;
			foreach (var c in name) {
				if (c >= 0x20) {
					allControlChars = false;
					break;
				}
			}
			if (allControlChars)
				return true;
			return dummyNameRegex.IsMatch(name);
		}

		/// <summary>
		/// A wrapper is a method that only loads its arguments and calls (or creates) one other
		/// method, then returns
		/// </summary>
		public static IMethod? TryGetWrapperTarget(MethodDef method) {
			if (!method.HasBody)
				return null;
			if (method.IsConstructor || method.IsStaticConstructor || method.IsPinvokeImpl || method.IsAbstract)
				return null;
			var body = method.Body;
			if (body.HasExceptionHandlers || body.Variables.Count > 0)
				return null;

			IMethod? target = null;
			int callCount = 0;
			bool hasReturn = false;
			foreach (var instr in body.Instructions) {
				switch (instr.OpCode.Code) {
				case Code.Nop:
				case Code.Break:
				case Code.Tailcall:
				case Code.Readonly:
				case Code.Constrained:
					break;

				case Code.Ldarg_0:
				case Code.Ldarg_1:
				case Code.Ldarg_2:
				case Code.Ldarg_3:
				case Code.Ldarg:
				case Code.Ldarg_S:
				case Code.Ldnull:
				case Code.Ldc_I4:
				case Code.Ldc_I4_0:
				case Code.Ldc_I4_1:
				case Code.Ldc_I4_2:
				case Code.Ldc_I4_3:
				case Code.Ldc_I4_4:
				case Code.Ldc_I4_5:
				case Code.Ldc_I4_6:
				case Code.Ldc_I4_7:
				case Code.Ldc_I4_8:
				case Code.Ldc_I4_M1:
				case Code.Ldc_I4_S:
				case Code.Ldc_R4:
				case Code.Ldc_R8:
					break;

				case Code.Call:
				case Code.Callvirt:
				case Code.Newobj:
					if (callCount > 0)
						return null;
					if (instr.Operand is not IMethod calledMethod)
						return null;
					target = calledMethod;
					callCount++;
					break;

				case Code.Ret:
					hasReturn = true;
					break;

				default:
					return null;
				}

				if (hasReturn && instr.OpCode.Code != Code.Ret)
					return null;
			}

			if (callCount != 1 || !hasReturn || target is null)
				return null;
			// The last instruction must be the ret
			var instructions = body.Instructions;
			if (instructions.Count == 0 || instructions[instructions.Count - 1].OpCode.Code != Code.Ret)
				return null;
			return target;
		}

		static string? GetProposedName(MethodDef method) {
			if (!IsDummyName(method.Name))
				return null;
			var target = TryGetWrapperTarget(method);
			if (target is null)
				return null;

			var typeName = target.DeclaringType?.Name ?? string.Empty;
			var index = typeName.IndexOf('`');
			if (index >= 0)
				typeName = typeName.Substring(0, index);

			// A wrapper around a constructor, eg. new FileStream(...)
			if (target.Name == ".ctor") {
				if (typeName.Length == 0)
					return null;
				var ctorName = MakeUnique(method, SanitizeIdentifier("New_" + typeName));
				return string.Equals(ctorName, method.Name, System.StringComparison.Ordinal) ? null : ctorName;
			}

			string targetName = target is MethodDef targetDef && targetDef.ImplMap is ImplMap implMap ?
				(string)implMap.Name : StripAccessorPrefix((string)target.Name);
			if (string.IsNullOrEmpty(targetName))
				return null;

			var name = SanitizeIdentifier(typeName.Length > 0 ? $"{typeName}_{targetName}" : targetName);
			if (name.Length == 0)
				return null;
			name = MakeUnique(method, name);
			return string.Equals(name, method.Name, System.StringComparison.Ordinal) ? null : name;
		}

		/// <summary>
		/// get_Value -> Value, set_Value -> Value, ...
		/// </summary>
		static string StripAccessorPrefix(string name) {
			foreach (var prefix in new[] { "get_", "set_", "add_", "remove_" }) {
				if (name.Length > prefix.Length && name.StartsWith(prefix, System.StringComparison.Ordinal))
					return name.Substring(prefix.Length);
			}
			return name;
		}

		static string MakeUnique(MethodDef method, string name) {
			var type = method.DeclaringType;
			if (type is null)
				return name;
			var candidate = name;
			int n = 2;
			while (NameExists(type, method, candidate))
				candidate = $"{name}_{n++}";
			return candidate;
		}

		static bool NameExists(TypeDef type, MethodDef method, string name) {
			foreach (var m in type.Methods) {
				if (m != method && string.Equals(m.Name, name, System.StringComparison.Ordinal))
					return true;
			}
			return false;
		}

		internal static string SanitizeIdentifier(string name) {
			var sb = new StringBuilder(name.Length);
			foreach (var c in name) {
				if (char.IsLetterOrDigit(c) || c == '_')
					sb.Append(c);
				else if (sb.Length > 0 && sb[sb.Length - 1] != '_')
					sb.Append('_');
			}
			var s = sb.ToString().Trim('_');
			if (s.Length > 0 && char.IsDigit(s[0]))
				s = "_" + s;
			return s;
		}
	}
}
