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
using System.IO;
using System.Linq;
using System.Text;
using dnlib.IO;
using dnlib.PE;

namespace dndbg.Engine {
	sealed class ECallManager {
		/// <summary>
		/// true if we found the CLR module (mscorwks/clr.dll)
		/// </summary>
		public bool FoundClrModule {
			get { return foundClrModule; }
		}
		readonly bool foundClrModule;

		readonly Dictionary<string, ECFunc[]> classToFuncsDict = new Dictionary<string, ECFunc[]>(StringComparer.Ordinal);
		ulong clrDllBaseAddress;

		/// <summary>
		/// Constructor that can be used to test this class on some random clr file
		/// </summary>
		/// <param name="filename"></param>
		public ECallManager(string filename) {
			foundClrModule = true;
			Initialize(filename);
		}

		public ECallManager(int pid, string debuggeeVersion) {
			var modNames = GetClrModuleNames(debuggeeVersion);

			var process = Process.GetProcessById(pid);
			foundClrModule = false;
			foreach (ProcessModule mod in process.Modules) {
				if (IsClrModule(mod, modNames)) {
					foundClrModule = true;
					Initialize(mod.FileName);
					clrDllBaseAddress = (ulong)mod.BaseAddress.ToInt64();
					break;
				}
			}
			Debug.Assert(foundClrModule, "Couldn't find mscorwks/clr.dll");
		}

		static bool IsClrModule(ProcessModule mod, string[] clrNames) {
			var fname = Path.GetFileName(mod.FileName);
			return clrNames.Any(n => StringComparer.OrdinalIgnoreCase.Equals(n, fname));
		}

		static string[] GetClrModuleNames(string debuggeeVersion) {
			if (IsCLR2OrEarlier(debuggeeVersion))
				return clr20_module_names;
			return clr40_module_names;
		}
		static readonly string[] clr20_module_names = new string[] {
			"mscorwks.dll",
			"mscorsvr.dll",
		};
		static readonly string[] clr40_module_names = new string[] {
			"clr.dll",
			"coreclr.dll",
		};

		static bool IsCLR2OrEarlier(string debuggeeVersion) {
			return debuggeeVersion != null && (debuggeeVersion.StartsWith("v1.") || debuggeeVersion.StartsWith("v2."));
		}

		void Initialize(string filename) {
			try {
				using (var reader = new ECallListReader(filename)) {
					foreach (var ecc in reader.List) {
						var fname = ecc.FullName;
						bool b = classToFuncsDict.ContainsKey(fname);
						Debug.Assert(!b);
						if (!b)
							classToFuncsDict[fname] = ecc.Functions;
					}
				}
			}
			catch {
			}
		}

		public bool FindFunc(string classFullName, string methodName, out ulong methodAddr) {
			methodAddr = 0;
			ECFunc[] funcs;
			if (!classToFuncsDict.TryGetValue(classFullName, out funcs))
				return false;
			foreach (var func in funcs) {
				if (func.Name == methodName) {
					methodAddr = clrDllBaseAddress + func.FunctionRVA;
					return true;
				}
			}
			return false;
		}
	}

	enum FCFuncFlag : ushort {
		EndOfArray = 0x01,
		HasSignature = 0x02,
		Unreferenced = 0x04, // Suppress unused fcall check
		QCall = 0x08, // QCall - mscorlib.dll to mscorwks.dll transition implemented as PInvoke
	}

	[DebuggerDisplay("{FullName}")]
	struct ECClass {
		public readonly string Namespace;
		public readonly string Name;
		public readonly ECFunc[] Functions;
		public string FullName {
			get { return string.IsNullOrEmpty(Namespace) ? Name : Namespace + "." + Name; }
		}

		public ECClass(string ns, string name, ECFunc[] funcs) {
			this.Namespace = ns;
			this.Name = name;
			this.Functions = funcs;
		}
	}

	// Unfortunately this enum is different from sscli20's CorInfoIntrinsics
	enum CorInfoIntrinsics : byte {
		Illegal = byte.MaxValue,
	}

	enum DynamicID : byte {
		FastAllocateString,
		CtorCharArrayManaged,
		CtorCharArrayStartLengthManaged,
		CtorCharCountManaged,
		CtorCharPtrManaged,
		CtorCharPtrStartLengthManaged,
		InternalGetCurrentThread,
		InvalidDynamicFCallId = byte.MaxValue,
	}

	[DebuggerDisplay("{FunctionRVA} {Name}")]
	struct ECFunc {
		public readonly uint RecordRVA;
		public readonly uint Flags;
		public readonly uint FunctionRVA;
		public readonly string Name;
		public readonly uint MethodSigRVA;

		public bool HasSignature {
			get { return MethodSigRVA != 0; }
		}

		public bool IsUnreferenced {
			get { return (Flags & (uint)FCFuncFlag.Unreferenced) != 0; }
		}

		public bool IsQCall {
			get { return (Flags & (uint)FCFuncFlag.QCall) != 0; }
		}

		public CorInfoIntrinsics IntrinsicID {
			get { return (CorInfoIntrinsics)(Flags >> 16); }
		}

		public DynamicID DynamicID {
			get { return (DynamicID)(Flags >> 24); }
		}

		public ECFunc(uint recRva, uint flags, uint methRva, string name, uint sigRva) {
			this.RecordRVA = recRva;
			this.Flags = flags;
			this.FunctionRVA = methRva;
			this.Name = name;
			this.MethodSigRVA = sigRva;
		}
	}

	struct ECallListReader : IDisposable {
		readonly PEImage peImage;
		readonly IImageStream reader;
		readonly bool is32bit;
		readonly uint ptrSize;
		readonly uint endRva;
		readonly List<ECClass> list;
		TableFormat? tableFormat;

		public List<ECClass> List {
			get { return list; }
		}

		enum TableFormat {
			// .NET 2.0 to ???
			V1,
			// .NET 3.x and later
			V2,
		}

		public ECallListReader(string filename) {
			this.peImage = new PEImage(filename);
			this.reader = peImage.CreateFullStream();
			this.is32bit = peImage.ImageNTHeaders.OptionalHeader.Magic == 0x010B;
			this.ptrSize = is32bit ? 4U : 8;
			var last = peImage.ImageSectionHeaders[peImage.ImageSectionHeaders.Count - 1];
			this.endRva = (uint)last.VirtualAddress + last.VirtualSize;
			this.list = new List<ECClass>();
			this.tableFormat = null;
			Read();
		}

		ulong ReadPtr(long pos) {
			reader.Position = pos;
			return is32bit ? reader.ReadUInt32() : reader.ReadUInt64();
		}

		uint? ReadRva(long pos) {
			ulong ptr = ReadPtr(pos);
			ulong b = peImage.ImageNTHeaders.OptionalHeader.ImageBase;
			if (ptr == 0)
				return 0;
			if (ptr < b)
				return null;
			ptr -= b;
			return ptr >= endRva ? (uint?)null : (uint)ptr;
		}

		ImageSectionHeader FindSection(string name) {
			foreach (var sect in peImage.ImageSectionHeaders) {
				if (sect.DisplayName == name)
					return sect;
			}
			return null;
		}

		void Read() {
			// Refs: coreclr/src/vm/{ecalllist.h,mscorlib.cpp,ecall.h,ecall.cpp}

			long pos = 0;
			long end = reader.Length - (3 * ptrSize - 1);

			List<ECClass> eccList = new List<ECClass>();
			for (; pos <= end; pos += ptrSize) {
				tableFormat = null;
				var ecc = ReadECClass(pos, true);
				if (ecc == null)
					continue;
				for (long pos2 = pos; pos2 <= end; pos2 += 3 * ptrSize) {
					ecc = ReadECClass(pos2, false);
					if (ecc == null)
						break;
					eccList.Add(ecc.Value);
				}
				if (eccList.Count >= 20)
					break;
				eccList.Clear();
			}

			list.AddRange(eccList);
		}

		ECClass? ReadECClass(long pos, bool first) {
			if (pos + ptrSize * 3 > reader.Length)
				return null;

			var name = ReadAsciizIdPtr(pos);
			if (name == null)
				return null;
			var ns = ReadAsciizIdPtr(pos + ptrSize);
			if (ns == null)
				return null;
			var funcs = ReadECFuncs(ReadRva(pos + ptrSize * 2), first);
			if (funcs == null)
				return null;

			return new ECClass(ns, name, funcs);
		}

		ECFunc[] ReadECFuncs(uint? rva, bool first) {
			if (rva == null || rva.Value == 0)
				return null;
			var funcs = new List<ECFunc>();

			var pos = (long)peImage.ToFileOffset((RVA)rva.Value);
			if (tableFormat == null)
				InitializeTableFormat(pos);
			if (tableFormat == null)
				return null;
			var tblSize = tableFormat == TableFormat.V1 ? 5 * ptrSize : 3 * ptrSize;
			for (;;) {
				if (pos + ptrSize > reader.Length)
					return null;
				ulong flags = ReadPtr(pos);
				if ((flags & (ulong)FCFuncFlag.EndOfArray) != 0)
					break;
				bool hasSig = (flags & (ulong)FCFuncFlag.HasSignature) != 0;
				uint size = tblSize + (hasSig ? ptrSize : 0);
				if (pos + size > reader.Length)
					return null;

				uint? methRva;
				string name;
				if (tableFormat == TableFormat.V1) {
					methRva = ReadRva(pos + ptrSize * 1);
					ulong nullPtr1 = ReadPtr(pos + ptrSize * 2);
					ulong nullPtr2 = ReadPtr(pos + ptrSize * 3);
					name = ReadAsciizIdPtr(pos + ptrSize * 4);
					if (nullPtr1 != 0 || nullPtr2 != 0)
						return null;
				}
				else {
					Debug.Assert(tableFormat == TableFormat.V2);
					methRva = ReadRva(pos + ptrSize * 1);
					name = ReadAsciizIdPtr(pos + ptrSize * 2);
				}
				if (name == null || methRva == null)
					return null;
				if (methRva.Value != 0 && !IsCodeRva(methRva.Value))
					return null;
				uint sigRva = 0;
				if (hasSig) {
					var srva = ReadRva(pos + tblSize);
					if (srva == null || srva.Value == 0)
						return null;
					sigRva = srva.Value;
				}
				uint recRva = (uint)peImage.ToRVA((FileOffset)pos);
				funcs.Add(new ECFunc(recRva, (uint)flags, methRva.Value, name, sigRva));
				pos += size;
            }

			// A zero length array is allowed (eg. clr.dll 4.6.96.0) so we can't return null if we find one
			return funcs.ToArray();
		}

		void InitializeTableFormat(long pos) {
			if (pos + ptrSize > reader.Length)
				return;
			ulong flags = ReadPtr(pos);
			if ((flags & (ulong)FCFuncFlag.EndOfArray) != 0)
				return;

			bool hasSig = (flags & (ulong)FCFuncFlag.HasSignature) != 0;

			if (pos + ptrSize * (5 + (hasSig ? 1 : 0)) < reader.Length) {
				uint? methRva = ReadRva(pos + ptrSize * 1);
				ulong nullPtr1 = ReadPtr(pos + ptrSize * 2);
				ulong nullPtr2 = ReadPtr(pos + ptrSize * 3);
				var name = ReadAsciizIdPtr(pos + ptrSize * 4);
				if (nullPtr1 == 0 && nullPtr2 == 0 && name != null && methRva != null && (methRva.Value == 0 || IsCodeRva(methRva.Value))) {
					tableFormat = TableFormat.V1;
					return;
				}
			}


			if (pos + ptrSize * (3 + (hasSig ? 1 : 0)) < reader.Length) {
				uint? methRva = ReadRva(pos + ptrSize * 1);
				var name = ReadAsciizIdPtr(pos + ptrSize * 2);
				if (name != null && methRva != null && (methRva.Value == 0 || IsCodeRva(methRva.Value))) {
					tableFormat = TableFormat.V2;
					return;
				}
			}
		}

		bool IsCodeRva(uint rva) {
			if (rva == 0)
				return false;
			var textSect = FindSection(".text");
			if (textSect == null)
				return false;
			return (uint)textSect.VirtualAddress <= rva && rva < (uint)textSect.VirtualAddress + Math.Max(textSect.VirtualSize, textSect.SizeOfRawData);
		}

		string ReadAsciizIdPtr(long pos) {
			return ReadAsciizId(ReadRva(pos));
		}

		string ReadAsciizId(uint? rva) {
			if (rva == null || rva.Value == 0)
				return null;
			reader.Position = (long)peImage.ToFileOffset((RVA)rva.Value);
			var bytes = reader.ReadBytesUntilByte(0);
			const int MIN_ID_LEN = 2;
			const int MAX_ID_LEN = 256;
			if (bytes == null || bytes.Length < MIN_ID_LEN || bytes.Length > MAX_ID_LEN)
				return null;
			foreach (var b in bytes) {
				var ch = (char)b;
				if (!(('a' <= ch && ch <= 'z') || ('A' <= ch && ch <= 'Z') || ('0' <= ch && ch <= '9') || ch == '_' || ch == '.'))
					return null;
			}
			var s = Encoding.ASCII.GetString(bytes);
			if (char.IsNumber(s[0]))
				return null;
			return s;
		}

		public void Dispose() {
			if (peImage != null)
				peImage.Dispose();
		}
	}
}
