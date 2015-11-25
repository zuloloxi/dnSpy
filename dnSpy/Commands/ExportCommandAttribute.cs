﻿// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ICSharpCode.ILSpy {
	#region Toolbar
	public interface IToolbarCommandMetadata {
		string ToolTip { get; }
		string ToolbarIcon { get; }
		string ToolbarCategory { get; }
		double ToolbarOrder { get; }
		string ToolbarIconText { get; }
	}

	public interface IToolbarCommand : ICommand {
		/// <summary>
		/// true if it should be added to the toolbar
		/// </summary>
		bool IsVisible { get; }
	}

	public interface IToolbarItemCreator {
		object CreateToolbarItem();
	}

	[MetadataAttribute]
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class ExportToolbarCommandAttribute : ExportAttribute, IToolbarCommandMetadata {
		public ExportToolbarCommandAttribute()
			: base("ToolbarCommand", typeof(ICommand)) {
		}

		public string ToolTip { get; set; }
		public string ToolbarIcon { get; set; }
		public string ToolbarCategory { get; set; }
		public double ToolbarOrder { get; set; }
		public string ToolbarIconText { get; set; }
	}
	#endregion

	#region Main Menu
	public interface IMainMenuCommandMetadata {
		string MenuIcon { get; }
		string MenuHeader { get; }
		string Menu { get; }
		string MenuCategory { get; }
		string MenuInputGestureText { get; }
		double MenuOrder { get; }
	}

	public interface IMainMenuCommand {
		/// <summary>
		/// true if it should be added to the menu
		/// </summary>
		bool IsVisible { get; }
	}

	public interface IMainMenuCheckableCommand {
		/// <summary>
		/// null if it's not checkable. Else it returns the checked state
		/// </summary>
		bool? IsChecked { get; }

		/// <summary>
		/// Gets the binding or null. Only called when <see cref="IsChecked"/> is not null
		/// </summary>
		Binding Binding { get; }
	}

	public interface IMainMenuCommandInitialize {
		void Initialize(MenuItem menuItem);
	}

	public interface IMenuItemProvider {
		/// <summary>
		/// Creates all menu items
		/// </summary>
		/// <param name="cachedMenuItem">The cached menu item for this command handler. It can be
		/// ignored if this command's menu items can never be the first menu items in the menu. Else
		/// this menu item should be the first returned menu item by this method. This is required
		/// or the first menu item won't be highlighted when the menu is opened from the keyboard
		/// the first time after this method is called.</param>
		/// <returns></returns>
		IEnumerable<MenuItem> CreateMenuItems(MenuItem cachedMenuItem);
	}

	[MetadataAttribute]
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class ExportMainMenuCommandAttribute : ExportAttribute, IMainMenuCommandMetadata {
		public ExportMainMenuCommandAttribute()
			: base("MainMenuCommand", typeof(ICommand)) {
		}

		public string MenuIcon { get; set; }
		public string MenuHeader { get; set; }
		public string Menu { get; set; }
		public string MenuCategory { get; set; }
		public string MenuInputGestureText { get; set; }
		public double MenuOrder { get; set; }
	}
	#endregion
}
