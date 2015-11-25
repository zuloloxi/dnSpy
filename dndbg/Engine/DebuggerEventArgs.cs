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
using dndbg.DotNet;

namespace dndbg.Engine {
	public class DebuggerEventArgs : EventArgs {
		public new static readonly DebuggerEventArgs Empty = new DebuggerEventArgs();
	}

	public sealed class ModuleDebuggerEventArgs : DebuggerEventArgs {
		public readonly DnModule Module;
		public readonly bool Added;

		public ModuleDebuggerEventArgs(DnModule module, bool added) {
			this.Module = module;
			this.Added = added;
		}
	}

	public sealed class NameChangedDebuggerEventArgs : DebuggerEventArgs {
		public readonly DnAppDomain AppDomain;
		public readonly DnThread Thread;

		public NameChangedDebuggerEventArgs(DnAppDomain appDomain, DnThread thread) {
			this.AppDomain = appDomain;
			this.Thread = thread;
		}
	}

	public sealed class CorModuleDefCreatedEventArgs : DebuggerEventArgs {
		public DnModule Module { get; private set; }
		public CorModuleDef CorModuleDef { get; private set; }

		public CorModuleDefCreatedEventArgs(DnModule module, CorModuleDef corModuleDef) {
			this.Module = module;
			this.CorModuleDef = corModuleDef;
		}
	}
}
