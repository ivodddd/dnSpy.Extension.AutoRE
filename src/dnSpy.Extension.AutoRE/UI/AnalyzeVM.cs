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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using dnSpy.AutoRE.Analysis;
using dnSpy.Contracts.App;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.Tabs;
using dnSpy.Contracts.MVVM;
using dnSpy.Contracts.TreeView;

namespace dnSpy.AutoRE.UI {
	/// <summary>
	/// Tool window view model. It always analyzes the assembly that owns the node selected in
	/// the assembly explorer, it can't be changed from here.
	/// </summary>
	sealed class AnalyzeVM : ViewModelBase {
		readonly AutoREAnalyzerService analyzerService;
		readonly IDocumentTabService documentTabService;

		public ObservableCollection<MethodRowVM> Rows { get; }
		public ICollectionView RowsView { get; }

		public RelayCommand AnalyzeCommand { get; }
		public RelayCommand ApplyCommand { get; }
		public RelayCommand NavigateCommand { get; }
		public RelayCommand CancelCommand { get; }
		public RelayCommand ExportCommand { get; }

		public bool OnlyWrappers {
			get => onlyWrappers;
			set {
				if (onlyWrappers != value) {
					onlyWrappers = value;
					OnPropertyChanged();
					RowsView.Refresh();
				}
			}
		}
		bool onlyWrappers;

		public bool IncludeNonPrivate {
			get => includeNonPrivate;
			set {
				if (includeNonPrivate != value) {
					includeNonPrivate = value;
					OnPropertyChanged();
				}
			}
		}
		bool includeNonPrivate;

		public string Status {
			get => status;
			set {
				if (status != value) {
					status = value;
					OnPropertyChanged();
				}
			}
		}
		string status = "Select an assembly or any of its members in the assembly explorer and press Analyze.";

		public bool IsBusy {
			get => isBusy;
			private set {
				if (isBusy != value) {
					isBusy = value;
					OnPropertyChanged();
				}
			}
		}
		bool isBusy;

		public AnalyzeVM(AutoREAnalyzerService analyzerService, IDocumentTabService documentTabService) {
			this.analyzerService = analyzerService;
			this.documentTabService = documentTabService;

			Rows = new ObservableCollection<MethodRowVM>();
			RowsView = new ListCollectionView(Rows);
			RowsView.GroupDescriptions?.Add(new PropertyGroupDescription(nameof(MethodRowVM.PrimaryTag)));

			AnalyzeCommand = new RelayCommand(async a => await AnalyzeAsync(), a => !analyzerService.IsBusy && GetSelectedDocument() is not null);
			ApplyCommand = new RelayCommand(async a => await ApplyAsync(), a => !analyzerService.IsBusy && Rows.Any(r => r.IsChecked));
			NavigateCommand = new RelayCommand(a => Navigate(a as MethodRowVM ?? Rows.FirstOrDefault()));
			CancelCommand = new RelayCommand(a => analyzerService.Cancel(), a => analyzerService.IsBusy);
			ExportCommand = new RelayCommand(a => Export(), a => Rows.Count > 0);

			analyzerService.Changed += AnalyzerService_Changed;
			RebuildRows();
		}

		void AnalyzerService_Changed(object? sender, EventArgs e) {
			IsBusy = analyzerService.IsBusy;
			if (!analyzerService.IsBusy)
				RebuildRows();
			System.Windows.Input.CommandManager.InvalidateRequerySuggested();
		}

		IDsDocument? GetSelectedDocument() =>
			DocumentFinder.GetDocument(documentTabService.DocumentTreeView.TreeView.SelectedItems);

		public void Analyze() => _ = AnalyzeAsync();

		async Task AnalyzeAsync() {
			var document = GetSelectedDocument();
			if (document is null) {
				Status = "Select an assembly or one of its members in the assembly explorer first.";
				return;
			}
			Status = "Analyzing " + document.GetShortName() + "...";
			await analyzerService.AnalyzeAsync(document);
			if (analyzerService.LastError is not null)
				Status = "Error: " + analyzerService.LastError;
			else if (analyzerService.Cancelled)
				Status = "Analysis cancelled.";
			else
				Status = analyzerService.Result?.Summary ?? "Nothing found.";
		}

		async Task ApplyAsync() {
			var items = Rows.Where(r => r.IsChecked).Select(r => r.Analysis).ToArray();
			if (items.Length == 0) {
				Status = "No renames selected.";
				return;
			}

			int nonPrivateCount = items.Count(i => !i.IsPrivateOrInternal && i.ProposedName is not null && i.CanRename);
			if (IncludeNonPrivate && nonPrivateCount > 0) {
				var res = MsgBox.Instance.Show(
					$"Renaming {nonPrivateCount} public/protected method(s) can break other assemblies that reference them. Continue?",
					MsgBoxButton.Yes | MsgBoxButton.No);
				if (res != MsgBoxButton.Yes) {
					Status = "Renames cancelled.";
					return;
				}
			}

			int count = analyzerService.ApplyRenames(items, IncludeNonPrivate);
			if (count == 0) {
				Status = "Nothing was renamed.";
				return;
			}

			if (analyzerService.Document is not null) {
				documentTabService.RefreshModifiedDocument(analyzerService.Document);
				documentTabService.DocumentTreeView.TreeView.RefreshAllNodes();
			}

			var document = analyzerService.Document;
			if (document is not null)
				await analyzerService.AnalyzeAsync(document);
			Status = $"Renamed {count} method(s). Save the changes with File \u2192 Save Module.";
		}

		void Export() {
			var result = analyzerService.Result;
			if (result is null) {
				Status = "Nothing to export.";
				return;
			}
			var dialog = new Microsoft.Win32.SaveFileDialog {
				FileName = result.Name + "-autore.csv",
				DefaultExt = ".csv",
				Filter = "CSV (*.csv)|*.csv|All files (*.*)|*.*",
			};
			if (dialog.ShowDialog() != true)
				return;
			try {
				AnalysisExporter.WriteCsv(result, dialog.FileName);
				Status = $"Exported {result.Methods.Count} row(s) to {dialog.FileName}";
			}
			catch (Exception ex) {
				Status = "Export failed: " + ex.Message;
			}
		}

		public void Navigate(MethodRowVM? row) {
			if (row is null)
				return;
			try {
				var node = documentTabService.DocumentTreeView.FindNode(row.Analysis.Method);
				if (node is null)
					return;
				var treeView = documentTabService.DocumentTreeView.TreeView;
				treeView.SelectItems(new TreeNodeData[] { node });
				treeView.Focus();
				treeView.ScrollIntoView();
			}
			catch {
				// The node could have been removed while the analysis was running
			}
		}

		void RebuildRows() {
			Rows.Clear();
			var result = analyzerService.Result;
			if (result is null) {
				RowsView.Refresh();
				return;
			}
			foreach (var analysis in result.Methods
					.OrderBy(a => a.PrimaryTag)
					.ThenBy(a => a.TypeFullName, StringComparer.Ordinal)
					.ThenBy(a => a.CurrentName, StringComparer.Ordinal)) {
				Rows.Add(new MethodRowVM(analysis));
			}
			RowsView.Refresh();
		}
	}
}
