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
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace ICSharpCode.Decompiler.Disassembler {
	public sealed class InstructionReference : IEquatable<InstructionReference> {
		public readonly MethodDef Method;
		public readonly Instruction Instruction;

		public InstructionReference(MethodDef method, Instruction instr) {
			this.Method = method;
			this.Instruction = instr;
		}

		public bool Equals(InstructionReference other) {
			return other != null &&
				Method == other.Method &&
				Instruction == other.Instruction;
		}

		public override bool Equals(object obj) {
			return Equals(obj as InstructionReference);
		}

		public override int GetHashCode() {
			return Method.GetHashCode() ^ Instruction.GetHashCode();
		}
	}
}
