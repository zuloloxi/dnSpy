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
	public enum DebuggerProcessState {
		/// <summary>
		/// Creating the first process
		/// </summary>
		Starting,

		/// <summary>
		/// Process is about to run after having been stopped
		/// </summary>
		Continuing,

		/// <summary>
		/// Process is running
		/// </summary>
		Running,

		/// <summary>
		/// Process is temporarily stopped
		/// </summary>
		Stopped,

		/// <summary>
		/// All processes have terminated
		/// </summary>
		Terminated,
	}
}
