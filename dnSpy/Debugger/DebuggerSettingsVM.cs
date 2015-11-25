﻿/*
    Copyright (C) 2014-2015 de4dot@gmail.com

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
using System.Windows.Input;
using System.Xml.Linq;
using dndbg.Engine;
using dnSpy.Debugger.Dialogs;
using dnSpy.MVVM;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.Options;

namespace dnSpy.Debugger {
	[ExportOptionPage(Title = "Debugger", Order = 4)]
	sealed class DebuggerSettingsVMCreator : IOptionPageCreator {
		public OptionPage Create() {
			return new DebuggerSettingsVM(new PickFilename());
		}
	}

	sealed class DebuggerSettingsVM : OptionPage {
		readonly IPickFilename pickFilename;

		public DebuggerSettings Settings {
			get { return settings; }
		}
		DebuggerSettings settings;

		public EnumListVM BreakProcessTypeVM {
			get { return breakProcessTypeVM; }
		}
		readonly EnumListVM breakProcessTypeVM = new EnumListVM(DebugProcessVM.breakProcessTypeList);

		public BreakProcessType BreakProcessType {
			get { return (BreakProcessType)BreakProcessTypeVM.SelectedItem; }
			set { BreakProcessTypeVM.SelectedItem = value; }
		}

		public ICommand PickCoreCLRDbgShimFilenameCommand {
			get { return new RelayCommand(a => PickNewCoreCLRDbgShimFilename()); }
		}

		public DebuggerSettingsVM(IPickFilename pickFilename) {
			if (pickFilename == null)
				throw new ArgumentNullException();
			this.pickFilename = pickFilename;
		}

		void PickNewCoreCLRDbgShimFilename() {
			const string filter = "dbgshim.dll|dbgshim.dll|All files (*.*)|*.*"; ;
			var newFilename = pickFilename.GetFilename(Settings.CoreCLRDbgShimFilename, "exe", filter);
			if (newFilename == null)
				return;

			Settings.CoreCLRDbgShimFilename = newFilename;
		}

		public override void Load(DNSpySettings settings) {
			this.settings = DebuggerSettings.Instance.Clone();
			this.BreakProcessType = this.settings.BreakProcessType;
		}

		public override RefreshFlags Save(XElement root) {
			this.settings.BreakProcessType = this.BreakProcessType;
			DebuggerSettings.WriteNewSettings(root, this.settings);
			return RefreshFlags.None;
		}
	}
}
