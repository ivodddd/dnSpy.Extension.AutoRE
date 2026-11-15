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

using System.IO;
using System.Text;

namespace dnSpy.AutoRE.Analysis {
	/// <summary>
	/// Exports the analysis result so it can be used from other tools or diffed between builds
	/// </summary>
	static class AnalysisExporter {
		public static void WriteCsv(AnalysisResult result, string filename) {
			using (var writer = new StreamWriter(filename, false, new UTF8Encoding(true)))
				WriteCsv(result, writer);
		}

		public static void WriteCsv(AnalysisResult result, TextWriter writer) {
			writer.WriteLine("token,type,method,tags,access,proposed,apis");
			foreach (var m in result.Methods) {
				writer.Write("0x");
				writer.Write(m.Method.MDToken.Raw.ToString("X8"));
				writer.Write(',');
				writer.Write(Escape(m.TypeFullName));
				writer.Write(',');
				writer.Write(Escape(m.CurrentName));
				writer.Write(',');
				writer.Write(Escape(m.TagsText));
				writer.Write(',');
				writer.Write(Escape(m.Accessibility));
				writer.Write(',');
				writer.Write(Escape(m.ProposedName ?? string.Empty));
				writer.Write(',');
				writer.Write(Escape(m.ApisText));
				writer.WriteLine();
			}
		}

		static string Escape(string value) {
			if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
				return value;
			return "\"" + value.Replace("\"", "\"\"") + "\"";
		}
	}
}
