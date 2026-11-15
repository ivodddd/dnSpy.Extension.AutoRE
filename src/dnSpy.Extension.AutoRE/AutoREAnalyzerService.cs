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
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using dnlib.DotNet;
using dnSpy.AutoRE.Analysis;
using dnSpy.Contracts.Documents;

namespace dnSpy.AutoRE {
	/// <summary>
	/// Runs the analysis and keeps the last result. Shared by the tool window and the commands
	/// </summary>
	[Export]
	sealed class AutoREAnalyzerService {
		readonly TagEngine tagEngine = new TagEngine();

		public AnalysisResult? Result { get; private set; }
		public IDsDocument? Document { get; private set; }
		public bool IsBusy { get; private set; }
		public bool Cancelled { get; private set; }
		public string? LastError { get; private set; }

		CancellationTokenSource? cancellationTokenSource;

		public void Cancel() => cancellationTokenSource?.Cancel();

		/// <summary>
		/// Raised when <see cref="Result"/>, <see cref="IsBusy"/> or <see cref="LastError"/> changed
		/// </summary>
		public event EventHandler? Changed;

		public async Task AnalyzeAsync(IDsDocument document) {
			if (IsBusy)
				return;
			IsBusy = true;
			LastError = null;
			Cancelled = false;
			Changed?.Invoke(this, EventArgs.Empty);
			var cts = new CancellationTokenSource();
			cancellationTokenSource = cts;
			try {
				var modules = document.GetModules<ModuleDef>().ToArray();
				var name = document.GetShortName();
				var token = cts.Token;
				var result = await Task.Run(() => {
					var res = new AnalysisResult(name);
					foreach (var module in modules) {
						token.ThrowIfCancellationRequested();
						tagEngine.AnalyzeModule(module, res, token);
					}
					token.ThrowIfCancellationRequested();
					return res;
				}, token);
				result.Summary = $"{result.Methods.Count} interesting method(s): " +
					$"{result.TaggedCount} tagged, {result.WrapperCount} wrapper(s)";
				Document = document;
				Result = result;
			}
			catch (OperationCanceledException) {
				Cancelled = true;
			}
			catch (Exception ex) {
				LastError = ex.Message;
			}
			finally {
				cancellationTokenSource = null;
				cts.Dispose();
				IsBusy = false;
				Changed?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// Renames the methods in <paramref name="items"/> that have a proposed name
		/// </summary>
		/// <returns>Number of renamed methods</returns>
		public int ApplyRenames(IEnumerable<MethodAnalysis> items, bool includeNonPrivate) {
			int count = 0;
			foreach (var item in items) {
				var newName = item.ProposedName;
				if (newName is null || !item.CanRename)
					continue;
				if (!includeNonPrivate && !item.IsPrivateOrInternal)
					continue;
				if (string.Equals(item.Method.Name, newName, StringComparison.Ordinal))
					continue;
				item.Method.Name = newName;
				count++;
			}
			if (count > 0)
				Changed?.Invoke(this, EventArgs.Empty);
			return count;
		}
	}
}
