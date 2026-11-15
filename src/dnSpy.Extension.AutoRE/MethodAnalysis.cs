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
using System.Linq;
using dnlib.DotNet;

namespace dnSpy.AutoRE.Analysis {
	/// <summary>
	/// Analysis result of one method
	/// </summary>
	sealed class MethodAnalysis {
		public MethodDef Method { get; }
		public string CurrentName { get; }
		public string TypeName { get; }
		public string TypeFullName { get; }
		public ApiTag Tags { get; }
		public string TagsText { get; }
		public string PrimaryTag { get; }
		public IReadOnlyList<string> Apis { get; }
		public string ApisText { get; }
		/// <summary>
		/// New name if this method is a simple wrapper and its name looks obfuscated
		/// </summary>
		public string? ProposedName { get; }
		public bool IsWrapper => ProposedName is not null;
		public bool IsPInvoke { get; }
		public bool CanRename { get; }
		public bool IsPrivateOrInternal => Method.IsPrivate || Method.IsAssembly;
		public string Accessibility {
			get {
				if (Method.IsPublic)
					return "public";
				if (Method.IsFamilyOrAssembly)
					return "protected internal";
				if (Method.IsFamily)
					return "protected";
				if (Method.IsAssembly)
					return "internal";
				return "private";
			}
		}

		public MethodAnalysis(MethodDef method, ApiTag tags, IReadOnlyList<string> apis, string? proposedName, bool canRename) {
			Method = method;
			CurrentName = method.Name;
			TypeName = GetTypeName(method.DeclaringType);
			TypeFullName = method.DeclaringType?.FullName ?? string.Empty;
			Tags = tags;
			TagsText = tags.ToDisplayString();
			PrimaryTag = (tags.GetPrimaryTag() ?? ApiTag.None).GetName();
			Apis = apis;
			ApisText = string.Join(", ", apis.Take(6));
			ProposedName = proposedName;
			IsPInvoke = method.IsPinvokeImpl;
			CanRename = canRename;
		}

		static string GetTypeName(ITypeDefOrRef? type) {
			var name = type?.Name ?? string.Empty;
			var index = name.IndexOf('`');
			if (index >= 0)
				name = name.Substring(0, index);
			return name;
		}

		public override string ToString() => $"{TypeFullName}::{CurrentName} [{TagsText}]";
	}

	/// <summary>
	/// Analysis result of one or more modules
	/// </summary>
	sealed class AnalysisResult {
		public string Name { get; }
		public List<MethodAnalysis> Methods { get; }
		public int WrapperCount => Methods.Count(a => a.IsWrapper);
		public int TaggedCount => Methods.Count(a => a.Tags != ApiTag.None);
		public string? Summary { get; set; }

		public AnalysisResult(string name) {
			Name = name;
			Methods = new List<MethodAnalysis>();
		}
	}
}
