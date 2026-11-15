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
using System.Collections.Generic;
using System.Linq;

namespace dnSpy.AutoRE.Analysis {
	/// <summary>
	/// What a member does, based on the APIs it uses
	/// </summary>
	[Flags]
	enum ApiTag {
		None = 0,
		File = 1 << 0,
		Registry = 1 << 1,
		Network = 1 << 2,
		Crypto = 1 << 3,
		Process = 1 << 4,
		Threading = 1 << 5,
		Memory = 1 << 6,
		Native = 1 << 7,
		AntiDebug = 1 << 8,
		Reflection = 1 << 9,
		Serialization = 1 << 10,
		Ui = 1 << 11,
		Environment = 1 << 12,
		Text = 1 << 13,
		Security = 1 << 14,
		Debug = 1 << 15,
	}

	static class ApiTagExtensions {
		// The order in which tags are shown. Tags that are more interesting when reversing
		// come first.
		static readonly ApiTag[] tagOrder = new ApiTag[] {
			ApiTag.AntiDebug,
			ApiTag.Crypto,
			ApiTag.Network,
			ApiTag.Registry,
			ApiTag.File,
			ApiTag.Process,
			ApiTag.Security,
			ApiTag.Memory,
			ApiTag.Native,
			ApiTag.Reflection,
			ApiTag.Serialization,
			ApiTag.Threading,
			ApiTag.Ui,
			ApiTag.Environment,
			ApiTag.Text,
			ApiTag.Debug,
		};

		public static IEnumerable<ApiTag> GetTags(this ApiTag tags) {
			foreach (var tag in tagOrder) {
				if ((tags & tag) != 0)
					yield return tag;
			}
		}

		/// <summary>
		/// Gets the most interesting tag or null if no tag is set
		/// </summary>
		public static ApiTag? GetPrimaryTag(this ApiTag tags) {
			foreach (var tag in tagOrder) {
				if ((tags & tag) != 0)
					return tag;
			}
			return null;
		}

		public static string GetName(this ApiTag tag) {
			switch (tag) {
			case ApiTag.File: return "FILE";
			case ApiTag.Registry: return "REG";
			case ApiTag.Network: return "NET";
			case ApiTag.Crypto: return "CRYPTO";
			case ApiTag.Process: return "PROC";
			case ApiTag.Threading: return "THREAD";
			case ApiTag.Memory: return "MEM";
			case ApiTag.Native: return "NATIVE";
			case ApiTag.AntiDebug: return "ANTI-DEBUG";
			case ApiTag.Reflection: return "REFLECT";
			case ApiTag.Serialization: return "SERIALIZE";
			case ApiTag.Ui: return "UI";
			case ApiTag.Environment: return "ENV";
			case ApiTag.Text: return "TEXT";
			case ApiTag.Security: return "SECURITY";
			case ApiTag.Debug: return "DEBUG";
			case ApiTag.None: return "NONE";
			default: return tag.ToString().ToUpperInvariant();
			}
		}

		public static string ToDisplayString(this ApiTag tags) =>
			string.Join(", ", tags.GetTags().Select(a => a.GetName()));
	}
}
