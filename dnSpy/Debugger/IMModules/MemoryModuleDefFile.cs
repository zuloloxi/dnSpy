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
using System.Collections.Generic;
using System.Diagnostics;
using dndbg.Engine;
using dnlib.DotNet;
using dnlib.PE;
using dnSpy.Files;

namespace dnSpy.Debugger.IMModules {
	/// <summary>
	/// A class that reads the module from the debugged process' address space.
	/// </summary>
	sealed class MemoryModuleDefFile : DotNetFileBase {
		sealed class MyKey : IDnSpyFilenameKey {
			readonly DnProcess process;
			readonly ulong address;

			public MyKey(DnProcess process, ulong address) {
				this.process = process;
				this.address = address;
			}

			public override bool Equals(object obj) {
				var o = obj as MyKey;
				return o != null && process == o.process && address == o.address;
			}

			public override int GetHashCode() {
				return process.GetHashCode() ^ (int)address ^ (int)(address >> 32);
			}
		}

		public override bool CanBeSavedToSettingsFile {
			get { return false; }
		}

		public override IDnSpyFilenameKey Key {
			get { return CreateKey(process, address); }
		}

		public override SerializedDnSpyModule? SerializedDnSpyModule {
			get {
				if (!isInMemory)
					return base.SerializedDnSpyModule;
				return Files.SerializedDnSpyModule.CreateInMemory(ModuleDef);
			}
		}

		public bool AutoUpdateMemory {
			get { return autoUpdateMemory; }
			set { autoUpdateMemory = value; }
		}
		bool autoUpdateMemory;

		public DnProcess Process {
			get { return process; }
		}
		readonly DnProcess process;

		internal Dictionary<ModuleDef, MemoryModuleDefFile> Dictionary {
			get { return dict; }
		}
		readonly Dictionary<ModuleDef, MemoryModuleDefFile> dict;

		public ulong Address {
			get { return address; }
		}
		readonly ulong address;

		readonly byte[] data;
		readonly bool isInMemory;

		MemoryModuleDefFile(Dictionary<ModuleDef, MemoryModuleDefFile>  dict, DnProcess process, ulong address, byte[] data, bool isInMemory, ModuleDef module, bool loadSyms, bool autoUpdateMemory)
			: base(module, loadSyms) {
			this.dict = dict;
			this.process = process;
			this.address = address;
			this.data = data;
			this.isInMemory = isInMemory;
			this.autoUpdateMemory = autoUpdateMemory;
		}

		public override bool LoadedFromFile {
			get { return false; }
		}

		public override bool IsReadOnly {
			get { return !process.HasExited; }
		}

		public static IDnSpyFilenameKey CreateKey(DnProcess process, ulong address) {
			return new MyKey(process, address);
		}

		public bool UpdateMemory() {
			if (process.HasExited)
				return false;
			//TODO: Only compare the smallest possible region, eg. all MD and IL bodies. Don't include writable sects.
			var newData = new byte[data.Length];
			ProcessMemoryUtils.ReadMemory(process, address, newData, 0, data.Length);
			if (Equals(data, newData))
				return false;
			Array.Copy(newData, data, data.Length);
			return true;
		}

		static bool Equals(byte[] a, byte[] b) {
			if (a == b)
				return true;
			if (a == null || b == null)
				return false;
			if (a.Length != b.Length)
				return false;
			for (int i = 0; i < a.Length; i++) {
				if (a[i] != b[i])
					return false;
			}
			return true;
		}

		public static MemoryModuleDefFile Create(Dictionary<ModuleDef, MemoryModuleDefFile>  dict, DnModule dnModule, bool loadSyms) {
			Debug.Assert(!dnModule.IsDynamic);
			Debug.Assert(dnModule.Address != 0);
			ulong address = dnModule.Address;
			var process = dnModule.Process;
			var data = new byte[dnModule.Size];
			string location = dnModule.IsInMemory ? string.Empty : dnModule.Name;

			ProcessMemoryUtils.ReadMemory(process, address, data, 0, data.Length);

			var peImage = new PEImage(data, GetImageLayout(dnModule), true);
			var module = ModuleDefMD.Load(peImage);
			module.Location = location;
			bool autoUpdateMemory = false;//TODO: Init to default value
			if (GacInfo.IsGacPath(dnModule.Name))
				autoUpdateMemory = false;	// GAC files are not likely to decrypt methods in memory
			return new MemoryModuleDefFile(dict, process, address, data, dnModule.IsInMemory, module, loadSyms, autoUpdateMemory);
		}

		static ImageLayout GetImageLayout(DnModule module) {
			Debug.Assert(!module.IsDynamic);
			return module.IsInMemory ? ImageLayout.File : ImageLayout.Memory;
		}

		public override DnSpyFile CreateDnSpyFile(ModuleDef module) {
			if (module == null)
				return null;
			MemoryModuleDefFile file;
			dict.TryGetValue(module, out file);
			return file;
		}
	}
}
