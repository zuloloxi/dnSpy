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
using dndbg.COM.CorDebug;

namespace dndbg.Engine {
	public sealed class CorStepper : COMObject<ICorDebugStepper>, IEquatable<CorStepper> {
		/// <summary>
		/// true if the stepper is active
		/// </summary>
		public bool IsActive {
			get {
				int active;
				int hr = obj.IsActive(out active);
				return hr >= 0 && active != 0;
			}
		}

		public CorStepper(ICorDebugStepper stepper)
			: base(stepper) {
		}

		public bool Deactivate() {
			int hr = obj.Deactivate();
			return hr >= 0;
		}

		public bool SetInterceptMask(CorDebugIntercept flags) {
			int hr = obj.SetInterceptMask(flags);
			return hr >= 0;
		}

		public bool SetUnmappedStopMask(CorDebugUnmappedStop flags) {
			int hr = obj.SetUnmappedStopMask(flags);
			return hr >= 0;
		}

		public bool Step(bool stepInto) {
			int hr = obj.Step(stepInto ? 1 : 0);
			return hr >= 0;
		}

		public bool StepRange(bool stepInto, StepRange[] ranges) {
			int hr = obj.StepRange(stepInto ? 1 : 0, ranges, (uint)ranges.Length);
			return hr >= 0;
		}

		public bool StepOut() {
			int hr = obj.StepOut();
			return hr >= 0;
		}

		public bool SetRangeIL(bool isIL) {
			int hr = obj.SetRangeIL(isIL ? 1 : 0);
			return hr >= 0;
		}

		public bool SetJMC(bool jmc) {
			var s2 = obj as ICorDebugStepper2;
			if (s2 == null)
				return true;
			int hr = s2.SetJMC(jmc ? 1 : 0);
			return hr >= 0;
		}

		public static bool operator ==(CorStepper a, CorStepper b) {
			if (ReferenceEquals(a, b))
				return true;
			if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
				return false;
			return a.Equals(b);
		}

		public static bool operator !=(CorStepper a, CorStepper b) {
			return !(a == b);
		}

		public bool Equals(CorStepper other) {
			return !ReferenceEquals(other, null) &&
				RawObject == other.RawObject;
		}

		public override bool Equals(object obj) {
			return Equals(obj as CorStepper);
		}

		public override int GetHashCode() {
			return RawObject.GetHashCode();
		}

		public override string ToString() {
			return string.Format("[Stepper] HC={0:X8} Active={1}", GetHashCode(), IsActive);
		}
	}
}
