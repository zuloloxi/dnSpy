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
using dndbg.COM.CorDebug;
using dndbg.COM.MetaData;

namespace dndbg.Engine {
	public sealed class CorClass : COMObject<ICorDebugClass>, IEquatable<CorClass> {
		/// <summary>
		/// Gets the token
		/// </summary>
		public uint Token {
			get { return token; }
		}
		readonly uint token;

		/// <summary>
		/// Gets the module or null
		/// </summary>
		public CorModule Module {
			get {
				ICorDebugModule module;
				int hr = obj.GetModule(out module);
				return hr < 0 || module == null ? null : new CorModule(module);
			}
		}

		/// <summary>
		/// true if this is <c>System.Enum</c>
		/// </summary>
		public bool IsSystemEnum {
			get { return IsSystem("Enum"); }
		}

		/// <summary>
		/// true if this is <c>System.ValueType</c>
		/// </summary>
		public bool IsSystemValueType {
			get { return IsSystem("ValueType"); }
		}

		/// <summary>
		/// true if this is <c>System.Object</c>
		/// </summary>
		public bool IsSystemObject {
			get { return IsSystem("Object"); }
		}

		/// <summary>
		/// true if this is <c>System.Decimal</c>
		/// </summary>
		public bool IsSystemDecimal {
			get { return IsSystem("Decimal"); }
		}

		public CorClass(ICorDebugClass cls)
			: base(cls) {
			int hr = cls.GetToken(out this.token);
			if (hr < 0)
				this.token = 0;
		}

		/// <summary>
		/// Creates a <see cref="CorType"/>
		/// </summary>
		/// <param name="etype">Element type, must be <see cref="CorElementType.Class"/> or <see cref="CorElementType.ValueType"/></param>
		/// <param name="typeArgs">Generic type arguments or null</param>
		/// <returns></returns>
		public CorType GetParameterizedType(CorElementType etype, CorType[] typeArgs = null) {
			Debug.Assert(etype == CorElementType.Class || etype == CorElementType.ValueType);
			var c2 = obj as ICorDebugClass2;
			if (c2 == null)
				return null;
			ICorDebugType value;
			int hr = c2.GetParameterizedType(etype, typeArgs == null ? 0 : typeArgs.Length, typeArgs.ToCorDebugArray(), out value);
			return hr < 0 || value == null ? null : new CorType(value);
		}

		/// <summary>
		/// Returns true if it's a System.XXX type in the corlib (eg. mscorlib)
		/// </summary>
		/// <param name="name">Name (not including namespace)</param>
		/// <returns></returns>
		public bool IsSystem(string name) {
			var mod = Module;
			if (mod == null)
				return false;
			var names = MetaDataUtils.GetTypeDefFullNames(mod.GetMetaDataInterface<IMetaDataImport>(), Token);
			if (names.Count != 1)
				return false;
			if (names[0].Name != "System." + name)
				return false;

			//TODO: Check if it's mscorlib

			return true;
		}

		/// <summary>
		/// Reads a static field
		/// </summary>
		/// <param name="token">Token of field</param>
		/// <param name="frame">Frame</param>
		/// <returns></returns>
		public CorValue GetStaticFieldValue(uint token, CorFrame frame) {
			int hr;
			return GetStaticFieldValue(token, frame, out hr);
		}

		/// <summary>
		/// Reads a static field
		/// </summary>
		/// <param name="token">Token of field</param>
		/// <param name="frame">Frame</param>
		/// <param name="hr">Updated with HRESULT</param>
		/// <returns></returns>
		public CorValue GetStaticFieldValue(uint token, CorFrame frame, out int hr) {
			ICorDebugValue value;
			hr = obj.GetStaticFieldValue(token, frame == null ? null : frame.RawObject, out value);
			return hr < 0 || value == null ? null : new CorValue(value);
		}

		/// <summary>
		/// Mark all methods in the type as user code
		/// </summary>
		/// <param name="jmc">true to set user code</param>
		/// <returns></returns>
		public bool SetJustMyCode(bool jmc) {
			var c2 = obj as ICorDebugClass2;
			if (c2 == null)
				return false;
			int hr = c2.SetJMCStatus(jmc ? 1 : 0);
			return hr >= 0;
		}

		/// <summary>
		/// Gets type generic parameters
		/// </summary>
		/// <returns></returns>
		public List<TokenAndName> GetGenericParameters() {
			var module = Module;
			return MetaDataUtils.GetGenericParameterNames(module == null ? null : module.GetMetaDataInterface<IMetaDataImport>(), Token);
		}

		/// <summary>
		/// Returns true if an attribute is present
		/// </summary>
		/// <param name="attributeName">Full name of attribute type</param>
		/// <returns></returns>
		public bool HasAttribute(string attributeName) {
			var mod = Module;
			var mdi = mod == null ? null : mod.GetMetaDataInterface<IMetaDataImport>();
			return MDAPI.HasAttribute(mdi, Token, attributeName);
		}

		public static bool operator ==(CorClass a, CorClass b) {
			if (ReferenceEquals(a, b))
				return true;
			if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
				return false;
			return a.Equals(b);
		}

		public static bool operator !=(CorClass a, CorClass b) {
			return !(a == b);
		}

		public bool Equals(CorClass other) {
			return !ReferenceEquals(other, null) &&
				RawObject == other.RawObject;
		}

		public override bool Equals(object obj) {
			return Equals(obj as CorClass);
		}

		public override int GetHashCode() {
			return RawObject.GetHashCode();
		}

		public T Write<T>(T output, TypePrinterFlags flags) where T : ITypeOutput {
			new TypePrinter(output, flags).Write(this);
			return output;
		}

		public string ToString(TypePrinterFlags flags) {
			return Write(new StringBuilderTypeOutput(), flags).ToString();
		}

		public override string ToString() {
			return ToString(TypePrinterFlags.Default);
		}
	}
}
