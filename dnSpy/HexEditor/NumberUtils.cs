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

namespace dnSpy.HexEditor {
	static class NumberUtils {
		public static ulong AddUInt64(ulong a, ulong b) {
			ulong res = a + b;
			if (res < a)
				return ulong.MaxValue;
			return res;
		}

		public static ulong SubUInt64(ulong a, ulong b) {
			ulong res = a - b;
			if (res > a)
				return ulong.MinValue;
			return res;
		}

		public static ulong MulUInt64(ulong a, ulong b) {
			// a*b <= ulong.MaxValue -> a <= ulong.MaxValue / b if b != 0
			// a*0 <= ulong.MaxValue if b == 0
			if (b == 0 || a <= ulong.MaxValue / b)
				return a * b;
			return ulong.MaxValue;
		}
	}
}
