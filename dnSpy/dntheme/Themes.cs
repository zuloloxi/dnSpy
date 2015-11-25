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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using ICSharpCode.ILSpy;
using Microsoft.Win32;

namespace dnSpy.dntheme {
	public sealed class HighContrastEventArgs : EventArgs {
		public bool IsHighContrast { get; private set; }

		public HighContrastEventArgs(bool isHighContrast) {
			this.IsHighContrast = isHighContrast;
		}
	}

	public static class Themes {
		static Dictionary<string, Theme> themes = new Dictionary<string, Theme>();

		static Theme theme;
		public static Theme Theme {
			get { return theme; }
			set {
				if (theme != value) {
					theme = value;
					if (ThemeChanged != null)
						ThemeChanged(null, EventArgs.Empty);
				}
			}
		}

		public static event EventHandler<EventArgs> ThemeChanged;
		public static event EventHandler<HighContrastEventArgs> IsHighContrastChanged;

		public static IEnumerable<Theme> AllThemesSorted {
			get { return themes.Values.OrderBy(x => x.Sort); }
		}

		public static string DefaultThemeName {
			get { return "dark"; }
		}

		public static string DefaultHighContrastThemeName {
			get { return "hc"; }
		}

		public static string CurrentDefaultThemeName {
			get { return IsHighContrast ? DefaultHighContrastThemeName : DefaultThemeName; }
		}

		public static bool IsHighContrast {
			get { return isHighContrast; }
			private set {
				if (isHighContrast != value) {
					isHighContrast = value;
					if (IsHighContrastChanged != null)
						IsHighContrastChanged(null, new HighContrastEventArgs(IsHighContrast));
				}
			}
		}
		static bool isHighContrast;

		static Themes() {
			Load();
			SystemEvents.UserPreferenceChanged += (s, e) => IsHighContrast = SystemParameters.HighContrast;
			IsHighContrast = SystemParameters.HighContrast;
		}

		static void Load() {
			foreach (var basePath in GetDnthemePaths()) {
				string[] files;
				try {
					if (!Directory.Exists(basePath))
						continue;
					files = Directory.GetFiles(basePath, "*.dntheme", SearchOption.TopDirectoryOnly);
				}
				catch (IOException) {
					continue;
				}
				catch (UnauthorizedAccessException) {
					continue;
				}
				catch (SecurityException) {
					continue;
				}

				foreach (var filename in files)
					Load(filename);
			}
		}

		static IEnumerable<string> GetDnthemePaths() {
			yield return Path.Combine(Path.GetDirectoryName(typeof(Themes).Assembly.Location), "dntheme");
			yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dnSpy", "dntheme");
		}

		static Theme Load(string filename) {
			try {
				var root = XDocument.Load(filename).Root;
				if (root.Name != "theme")
					return null;

				var theme = new Theme(root);
				if (string.IsNullOrEmpty(theme.Name) || string.IsNullOrEmpty(theme.MenuName))
					return null;

				themes[theme.Name] = theme;
				return theme;
			}
			catch (Exception) {
				Debug.Fail(string.Format("Failed to load file '{0}'", filename));
			}
			return null;
		}

		public static Theme GetThemeOrDefault(string name) {
			var theme = themes[name] ?? themes[DefaultThemeName] ?? AllThemesSorted.FirstOrDefault();
			Debug.Assert(theme != null);
			return theme;
		}

		public static void SwitchThemeIfNecessary() {
			if (Theme.IsHighContrast != IsHighContrast)
				Theme = GetThemeOrDefault(CurrentDefaultThemeName);
		}
	}

	[ExportMainMenuCommand(Menu = "_Themes", MenuCategory = "Themes", MenuOrder = 4000)]
	sealed class ThemesMenu : ICommand, IMenuItemProvider {
		public ThemesMenu() {
			Themes.ThemeChanged += (s, e) => UpdateThemesMenu();
		}

		void UpdateThemesMenu() {
			MainWindow.Instance.UpdateMainSubMenu("_Themes");
		}

		public bool CanExecute(object parameter) {
			return false;
		}

		public event EventHandler CanExecuteChanged {
			add { }
			remove { }
		}

		public void Execute(object parameter) {
		}

		public IEnumerable<MenuItem> CreateMenuItems(MenuItem cachedMenuItem) {
			int index = 0;
			foreach (var theme in Themes.AllThemesSorted) {
				var item = index++ == 0 ? cachedMenuItem : new MenuItem();
				item.Header = theme.MenuName;
				item.IsChecked = theme == Themes.Theme;
				item.Command = new SetThemeCommand(theme);
				yield return item;
			}
		}

		sealed class SetThemeCommand : ICommand {
			readonly Theme theme;

			public SetThemeCommand(Theme theme) {
				this.theme = theme;
			}

			public bool CanExecute(object parameter) {
				return true;
			}

			public event EventHandler CanExecuteChanged {
				add { }
				remove { }
			}

			public void Execute(object parameter) {
				Themes.Theme = theme;
			}
		}
	}
}
