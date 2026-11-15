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

using dnSpy.AutoRE.Analysis;
using dnSpy.Contracts.MVVM;

namespace dnSpy.AutoRE.UI {
	/// <summary>
	/// One row in the results list
	/// </summary>
	sealed class MethodRowVM : ViewModelBase {
		public MethodAnalysis Analysis { get; }
		public string CurrentName => Analysis.CurrentName;
		public string ProposedName => Analysis.ProposedName ?? string.Empty;
		public string TagsText => Analysis.TagsText;
		public string Accessibility => Analysis.Accessibility;
		public string ApisText => Analysis.ApisText;
		public string PrimaryTag => Analysis.PrimaryTag;
		public string TypeFullName => Analysis.TypeFullName;

		public bool IsChecked {
			get => isChecked;
			set {
				if (isChecked != value) {
					isChecked = value;
					OnPropertyChanged();
				}
			}
		}
		bool isChecked;

		public MethodRowVM(MethodAnalysis analysis) {
			Analysis = analysis;
			isChecked = analysis.ProposedName is not null;
		}
	}
}
