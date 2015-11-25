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
using System.Globalization;
using System.Windows.Data;
using dnSpy.TreeNodes;

namespace dnSpy.Debugger.Locals {
	sealed class LocalColumnConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			var vm = value as ValueVM;
			var s = parameter as string;
			if (vm == null || s == null)
				return null;

			var gen = UISyntaxHighlighter.Create(DebuggerSettings.Instance.SyntaxHighlightLocals);
			var printer = new ValuePrinter(gen.TextOutput, DebuggerSettings.Instance.UseHexadecimal);
			if (StringComparer.OrdinalIgnoreCase.Equals(s, "Name"))
				printer.WriteName(vm);
			else if (StringComparer.OrdinalIgnoreCase.Equals(s, "Value"))
				printer.WriteValue(vm);
			else if (StringComparer.OrdinalIgnoreCase.Equals(s, "Type"))
				printer.WriteType(vm);
			else
				return null;

			return gen.CreateTextBlock(true);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
}
