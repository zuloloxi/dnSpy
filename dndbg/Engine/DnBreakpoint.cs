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

namespace dndbg.Engine {
	public abstract class BreakpointConditionContext {
		public abstract DnBreakpoint Breakpoint { get; }

		public DnDebugger Debugger {
			get { return debugger; }
		}
		readonly DnDebugger debugger;

		protected BreakpointConditionContext(DnDebugger debugger) {
			this.debugger = debugger;
		}
	}

	public abstract class DnBreakpoint {
		public IBreakpointCondition Condition {
			get { return bpCond; }
		}
		readonly IBreakpointCondition bpCond;

		/// <summary>
		/// The user can set this property to any value. It's not used by the debugger.
		/// </summary>
		public object Tag { get; set; }

		public bool IsEnabled {
			get { return isEnabled; }
			set {
				if (isEnabled != value) {
					isEnabled = value;
					OnIsEnabledChanged();
				}
			}
		}
		bool isEnabled = true;

		protected virtual void OnIsEnabledChanged() {
		}

		protected DnBreakpoint(IBreakpointCondition bpCond) {
			this.bpCond = bpCond ?? AlwaysBreakpointCondition.Instance;
		}

		internal virtual void OnRemoved() {
		}
	}
}
