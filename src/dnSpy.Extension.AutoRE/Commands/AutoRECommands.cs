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
using System.ComponentModel.Composition;
using System.Windows.Input;
using dnSpy.AutoRE.UI;
using dnSpy.Contracts.Controls;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.Tabs;
using dnSpy.Contracts.Extension;
using dnSpy.Contracts.Menus;
using dnSpy.Contracts.ToolWindows.App;
using dnSpy.Contracts.TreeView;

namespace dnSpy.AutoRE.Commands {
	static class AutoREConstants {
		// Order of the group, and the guid of the treeview context menu group
		public const string GROUP_TREEVIEW = "20000,8A2F5E9D-4C2B-4B4E-9C3A-6C0B3E5D7F10";
	}

	/// <summary>
	/// Shows the tool window and analyzes the assembly that owns the selected node
	/// </summary>
	sealed class AnalyzeRunner {
		readonly AutoREWindowService windowService;
		readonly IDsToolWindowService toolWindowService;

		public AnalyzeRunner(AutoREWindowService windowService, IDsToolWindowService toolWindowService) {
			this.windowService = windowService;
			this.toolWindowService = toolWindowService;
		}

		public void Show() {
			toolWindowService.Show(AnalyzeToolWindowContent.THE_GUID);
			windowService.VM.Analyze();
		}
	}

	[ExportMenuItem(Group = AutoREConstants.GROUP_TREEVIEW, Order = 60)]
	sealed class AnalyzeTreeViewCommand : MenuItemBase {
		readonly AnalyzeRunner runner;

		[ImportingConstructor]
		AnalyzeTreeViewCommand(AutoREWindowService windowService, IDsToolWindowService toolWindowService) =>
			runner = new AnalyzeRunner(windowService, toolWindowService);

		static bool HasDocument(IMenuItemContext context) => DocumentFinder.GetDocument(context.Find<TreeNodeData[]>()) is not null;

		public override bool IsVisible(IMenuItemContext context) =>
			context.CreatorObject.Guid == new Guid(MenuConstants.GUIDOBJ_DOCUMENTS_TREEVIEW_GUID);

		public override bool IsEnabled(IMenuItemContext context) => HasDocument(context);

		public override string? GetHeader(IMenuItemContext context) => "AutoRE: Analyze";

		public override void Execute(IMenuItemContext context) {
			if (HasDocument(context))
				runner.Show();
		}
	}

	[ExportMenuItem(OwnerGuid = MenuConstants.APP_MENU_EDIT_GUID, Header = "AutoRE: Analyze Assembly", Group = MenuConstants.GROUP_APP_MENU_EDIT_ASMED_MISC, Order = 100)]
	sealed class AnalyzeMainMenuCommand : MenuItemBase {
		readonly AnalyzeRunner runner;
		readonly IDocumentTabService documentTabService;

		[ImportingConstructor]
		AnalyzeMainMenuCommand(AutoREWindowService windowService, IDsToolWindowService toolWindowService, IDocumentTabService documentTabService) {
			runner = new AnalyzeRunner(windowService, toolWindowService);
			this.documentTabService = documentTabService;
		}

		IDsDocument? GetDocument() => DocumentFinder.GetDocument(documentTabService.DocumentTreeView.TreeView.SelectedItems);
		bool HasDocument => GetDocument() is not null;

		public override bool IsEnabled(IMenuItemContext context) => HasDocument;

		public override void Execute(IMenuItemContext context) {
			if (HasDocument)
				runner.Show();
		}
	}

	/// <summary>
	/// Ctrl+Alt+R analyzes the selected assembly
	/// </summary>
	[ExportAutoLoaded]
	sealed class AutoRECommandLoader : IAutoLoaded {
		static readonly RoutedCommand AnalyzeCommand = new RoutedCommand("AutoREAnalyzeCommand", typeof(AutoRECommandLoader));

		[ImportingConstructor]
		AutoRECommandLoader(IWpfCommandService wpfCommandService, AutoREWindowService windowService, IDsToolWindowService toolWindowService, IDocumentTabService documentTabService) {
			var runner = new AnalyzeRunner(windowService, toolWindowService);
			var cmds = wpfCommandService.GetCommands(ControlConstants.GUID_MAINWINDOW);
			cmds.Add(AnalyzeCommand,
				(s, e) => {
					if (DocumentFinder.GetDocument(documentTabService.DocumentTreeView.TreeView.SelectedItems) is not null)
						runner.Show();
				},
				(s, e) => e.CanExecute = DocumentFinder.GetDocument(documentTabService.DocumentTreeView.TreeView.SelectedItems) is not null,
				ModifierKeys.Control | ModifierKeys.Alt, Key.R);
		}
	}
}
