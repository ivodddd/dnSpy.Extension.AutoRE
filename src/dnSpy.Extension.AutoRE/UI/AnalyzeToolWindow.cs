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
using System.Windows;
using System.Windows.Input;
using dnSpy.Contracts.Documents.Tabs;
using dnSpy.Contracts.ToolWindows;
using dnSpy.Contracts.ToolWindows.App;

namespace dnSpy.AutoRE.UI {
	/// <summary>
	/// Owns the tool window content. The commands use it to start an analysis and the
	/// tool window provider uses it to create the tool window
	/// </summary>
	[Export]
	sealed class AutoREWindowService {
		public AnalyzeToolWindowContent Content { get; }
		public AnalyzeVM VM => Content.VM;

		[ImportingConstructor]
		AutoREWindowService(AutoREAnalyzerService analyzerService, IDocumentTabService documentTabService) =>
			Content = new AnalyzeToolWindowContent(analyzerService, documentTabService);
	}

	[Export(typeof(IToolWindowContentProvider))]
	sealed class AnalyzeToolWindowContentProvider : IToolWindowContentProvider {
		readonly AutoREWindowService windowService;

		[ImportingConstructor]
		AnalyzeToolWindowContentProvider(AutoREWindowService windowService) => this.windowService = windowService;

		public IEnumerable<ToolWindowContentInfo> ContentInfos {
			get { yield return new ToolWindowContentInfo(AnalyzeToolWindowContent.THE_GUID, AnalyzeToolWindowContent.DEFAULT_LOCATION, 0, false); }
		}

		public ToolWindowContent? GetOrCreate(Guid guid) =>
			guid == AnalyzeToolWindowContent.THE_GUID ? windowService.Content : null;
	}

	sealed class AnalyzeToolWindowContent : ToolWindowContent {
		public static readonly Guid THE_GUID = new Guid("3F0C7B21-9D64-4A1E-8B57-2C6E4D9A10F3");
		public const AppToolWindowLocation DEFAULT_LOCATION = AppToolWindowLocation.DefaultHorizontal;

		public override Guid Guid => THE_GUID;
		public override string Title => "AutoRE";
		public override object? UIObject => control;
		public override IInputElement? FocusedElement => control;
		public override FrameworkElement? ZoomElement => control;

		readonly AnalyzeToolWindowControl control;
		public AnalyzeVM VM { get; }

		public AnalyzeToolWindowContent(AutoREAnalyzerService analyzerService, IDocumentTabService documentTabService) {
			VM = new AnalyzeVM(analyzerService, documentTabService);
			control = new AnalyzeToolWindowControl { DataContext = VM };
		}

		public override void OnVisibilityChanged(ToolWindowContentVisibilityEvent visEvent) {
		}
	}
}
