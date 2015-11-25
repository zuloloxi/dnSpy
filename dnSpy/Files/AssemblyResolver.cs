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
using System.IO;
using dnlib.DotNet;
using ICSharpCode.ILSpy;

namespace dnSpy.Files {
	public sealed class AssemblyResolver : IAssemblyResolver {
		readonly DnSpyFileList fileList;

		static readonly Version invalidMscorlibVersion = new Version(255, 255, 255, 255);
		static readonly Version newMscorlibVersion = new Version(4, 0, 0, 0);

		public AssemblyResolver(DnSpyFileList fileList) {
			this.fileList = fileList;
		}

		public void AddSearchPath(string s) {
			lock (asmSearchPathsLockObj) {
				asmSearchPaths.Add(s);
				asmSearchPathsArray = asmSearchPaths.ToArray();
			}
		}
		readonly object asmSearchPathsLockObj = new object();
		readonly List<string> asmSearchPaths = new List<string>();
		string[] asmSearchPathsArray = new string[0];

		bool IAssemblyResolver.AddToCache(AssemblyDef asm) {
			return false;
		}

		void IAssemblyResolver.Clear() {
		}

		bool IAssemblyResolver.Remove(AssemblyDef asm) {
			return false;
		}

		AssemblyDef IAssemblyResolver.Resolve(IAssembly assembly, ModuleDef sourceModule) {
			var file = Resolve(assembly, sourceModule, true);
			return file == null ? null : file.AssemblyDef;
		}

		public DnSpyFile Resolve(IAssembly assembly, ModuleDef sourceModule = null, bool delayLoad = false) {
			FrameworkRedirect.ApplyFrameworkRedirect(ref assembly, sourceModule);

			if (assembly.IsContentTypeWindowsRuntime)
				return ResolveWinMD(assembly, sourceModule, delayLoad);

			// WinMD files have a reference to mscorlib but its version is always 255.255.255.255
			// since mscorlib isn't really loaded. The resolver only loads exact versions, so
			// we must change the version or the resolve will fail.
			if (assembly.Name == "mscorlib" && assembly.Version == invalidMscorlibVersion)
				assembly = new AssemblyNameInfo(assembly) { Version = newMscorlibVersion };

			return ResolveNormal(assembly, sourceModule, delayLoad);
		}

		DnSpyFile ResolveNormal(IAssembly assembly, ModuleDef sourceModule, bool delayLoad) {
			var existingFile = fileList.FindAssembly(assembly);
			if (existingFile != null)
				return existingFile;

			var file = LookupFromSearchPaths(assembly, sourceModule, true);
			if (file != null)
				return fileList.AddFile(file, fileList.AssemblyLoadEnabled, delayLoad);

			if (fileList.UseGAC) {
				var gacFile = GacInterop.FindAssemblyInNetGac(assembly);
				if (gacFile != null)
					return fileList.GetOrCreate(gacFile, fileList.AssemblyLoadEnabled, true, delayLoad);
				foreach (var path in GacInfo.OtherGacPaths) {
					file = TryLoadFromDir(assembly, true, path);
					if (file != null)
						return fileList.AddFile(file, fileList.AssemblyLoadEnabled, delayLoad);
				}
			}

			file = LookupFromSearchPaths(assembly, sourceModule, false);
			if (file != null)
				return fileList.AddFile(file, fileList.AssemblyLoadEnabled, delayLoad);

			return null;
		}

		DnSpyFile LookupFromSearchPaths(IAssembly asmName, ModuleDef sourceModule, bool exactCheck) {
			DnSpyFile file;
			string sourceModuleDir = null;
			if (sourceModule != null && File.Exists(sourceModule.Location)) {
				sourceModuleDir = Path.GetDirectoryName(sourceModule.Location);
				file = TryLoadFromDir(asmName, exactCheck, sourceModuleDir);
				if (file != null)
					return file;
			}
			var ary = asmSearchPathsArray;
			foreach (var path in ary) {
				file = TryLoadFromDir(asmName, exactCheck, path);
				if (file != null)
					return file;
			}

			return null;
		}

		DnSpyFile TryLoadFromDir(IAssembly asmName, bool exactCheck, string dirPath) {
			string baseName;
			try {
				baseName = Path.Combine(dirPath, asmName.Name);
			}
			catch (ArgumentException) { // eg. invalid chars in asmName.Name
				return null;
			}
			return TryLoadFromDir2(asmName, exactCheck, baseName + ".dll") ??
				   TryLoadFromDir2(asmName, exactCheck, baseName + ".exe");
		}

		DnSpyFile TryLoadFromDir2(IAssembly asmName, bool exactCheck, string filename) {
			if (!File.Exists(filename))
				return null;

			DnSpyFile file = null;
			bool error = true;
			try {
				file = fileList.CreateDnSpyFile(filename);
				file.IsAutoLoaded = true;
				var asm = file.AssemblyDef;
				if (asm == null)
					return null;
				bool b = exactCheck ?
					AssemblyNameComparer.CompareAll.Equals(asmName, asm) :
					AssemblyNameComparer.NameAndPublicKeyTokenOnly.Equals(asmName, asm);
				if (!b)
					return null;

				error = false;
				return file;
			}
			finally {
				if (error) {
					if (file != null)
						file.Dispose();
				}
			}
		}

		DnSpyFile ResolveWinMD(IAssembly assembly, ModuleDef sourceModule, bool delayLoad) {
			var existingFile = fileList.FindAssembly(assembly);
			if (existingFile != null)
				return existingFile;

			foreach (var winmdPath in GacInfo.WinmdPaths) {
				string file;
				try {
					file = Path.Combine(winmdPath, assembly.Name + ".winmd");
				}
				catch (ArgumentException) {
					continue;
				}
				if (File.Exists(file))
					return fileList.GetOrCreate(file, fileList.AssemblyLoadEnabled, true, delayLoad);
			}
			return null;
		}
	}
}
