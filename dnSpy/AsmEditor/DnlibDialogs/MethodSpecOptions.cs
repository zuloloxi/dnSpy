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

using System.Collections.Generic;
using dnlib.DotNet;
using ICSharpCode.ILSpy;

namespace dnSpy.AsmEditor.DnlibDialogs {
	sealed class MethodSpecOptions {
		public IMethodDefOrRef Method;
		public CallingConventionSig Instantiation;
		public List<CustomAttribute> CustomAttributes = new List<CustomAttribute>();

		public MethodSpecOptions() {
		}

		public MethodSpecOptions(MethodSpec ms) {
			this.Method = ms.Method;
			this.Instantiation = ms.Instantiation;
			this.CustomAttributes.AddRange(ms.CustomAttributes);
		}

		public MethodSpec CopyTo(MethodSpec ms) {
			ms.Method = this.Method;
			ms.Instantiation = this.Instantiation;
			ms.CustomAttributes.Clear();
			ms.CustomAttributes.AddRange(this.CustomAttributes);
			return ms;
		}

		public MethodSpec Create(ModuleDef ownerModule) {
			return ownerModule.UpdateRowId(CopyTo(new MethodSpecUser()));
		}
	}
}
