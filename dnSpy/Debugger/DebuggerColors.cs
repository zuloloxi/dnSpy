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

using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;

namespace dnSpy.Debugger {
	static class DebuggerColors {
		public static HighlightingColor CodeBreakpointHighlightingColor = new HighlightingColor {
			Background = new SimpleHighlightingBrush(Color.FromRgb(0xB4, 0x26, 0x26)),
			Foreground = new SimpleHighlightingBrush(Colors.White),
		};
		public static HighlightingColor CodeBreakpointDisabledHighlightingColor = CodeBreakpointHighlightingColor;
		public static HighlightingColor StackFrameReturnHighlightingColor = new HighlightingColor {
			Background = new SimpleHighlightingBrush(Color.FromArgb(0x62, 0xEE, 0xEF, 0xE6)),
			Foreground = new SimpleHighlightingBrush(Colors.Transparent),
		};
		public static HighlightingColor StackFrameSelectedHighlightingColor = new HighlightingColor {
			Background = new SimpleHighlightingBrush(Color.FromArgb(0x68, 0xB4, 0xE4, 0xB4)),
			Foreground = new SimpleHighlightingBrush(Colors.Black),
		};
		public static HighlightingColor StackFrameCurrentHighlightingColor = new HighlightingColor {
			Background = new SimpleHighlightingBrush(Colors.Yellow),
			Foreground = new SimpleHighlightingBrush(Colors.Blue),
		};

		static DebuggerColors() {
			dntheme.Themes.ThemeChanged += (s, e) => OnThemeUpdated();
			OnThemeUpdated();
		}

		static void OnThemeUpdated() {
			var theme = dntheme.Themes.Theme;
			CodeBreakpointHighlightingColor = theme.GetColor(dntheme.ColorType.BreakpointStatement).TextInheritedColor;
			CodeBreakpointDisabledHighlightingColor = theme.GetColor(dntheme.ColorType.DisabledBreakpointStatement).TextInheritedColor;
			StackFrameCurrentHighlightingColor = theme.GetColor(dntheme.ColorType.CurrentStatement).TextInheritedColor;
			StackFrameReturnHighlightingColor = theme.GetColor(dntheme.ColorType.ReturnStatement).TextInheritedColor;
			StackFrameSelectedHighlightingColor = theme.GetColor(dntheme.ColorType.SelectedReturnStatement).TextInheritedColor;
		}
	}
}
