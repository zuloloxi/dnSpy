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

using System.Diagnostics;
using dndbg.Engine;
using dnlib.PE;
using dnSpy.NRefactory;
using ICSharpCode.Decompiler;

namespace dnSpy.Debugger.Dialogs {
	sealed class ProcessPrinter {
		readonly ITextOutput output;
		readonly bool useHex;

		public ProcessPrinter(ITextOutput output, bool useHex) {
			this.output = output;
			this.useHex = useHex;
		}

		void WriteFilename(ProcessVM vm, string filename) {
			output.WriteFilename(filename);
		}

		public void WriteFilename(ProcessVM vm) {
			WriteFilename(vm, DebugOutputUtils.GetFilename(vm.FullPath));
		}

		public void WriteFullPath(ProcessVM vm) {
			WriteFilename(vm, vm.FullPath);
		}

		public void WritePID(ProcessVM vm) {
			if (useHex)
				output.Write(string.Format("0x{0:X8}", vm.PID), TextTokenType.Number);
			else
				output.Write(string.Format("{0}", vm.PID), TextTokenType.Number);
		}

		public void WriteCLRVersion(ProcessVM vm) {
			output.Write(vm.CLRVersion, TextTokenType.Number);
		}

		public void WriteType(ProcessVM vm) {
			output.Write(TypeToString(vm.CLRTypeInfo.CLRType), TextTokenType.EnumField);
		}

		static string TypeToString(CLRType type) {
			switch (type) {
			case CLRType.Desktop:	return "Desktop";
			case CLRType.CoreCLR:	return "CoreCLR";
			default:
				Debug.Fail("Unknown CLR type");
				return "???";
			}
		}

		public void WriteMachine(ProcessVM vm) {
			output.Write(ToString(vm.Machine), TextTokenType.InstanceMethod);
		}

		static string ToString(Machine machine) {
			switch (machine) {
			case Machine.I386:		return "x86";
			case Machine.AMD64:		return "x64";
			default:				return machine.ToString();
			}
		}

		public void WriteTitle(ProcessVM vm) {
			output.Write(vm.Title, TextTokenType.String);
		}
	}
}
