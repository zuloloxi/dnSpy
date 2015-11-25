﻿// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace ICSharpCode.ILSpy.XmlDoc {
	/// <summary>
	/// Provides XML documentation tags.
	/// </summary>
	public sealed class XmlDocKeyProvider {
		#region GetKey
		public static string GetKey(IMemberRef member) {
			if (member == null)
				return null;
			StringBuilder b = new StringBuilder();
			if (member is ITypeDefOrRef) {
				b.Append("T:");
				AppendTypeName(b, ((ITypeDefOrRef)member).ToTypeSig());
			}
			else {
				if (member.IsField)
					b.Append("F:");
				else if (member.IsPropertyDef)
					b.Append("P:");
				else if (member.IsEventDef)
					b.Append("E:");
				else if (member.IsMethod)
					b.Append("M:");
				AppendTypeName(b, member.DeclaringType.ToTypeSig());
				b.Append('.');
				b.Append(member.Name.Replace('.', '#'));
				IList<Parameter> parameters;
				TypeSig explicitReturnType = null;
				if (member.IsPropertyDef) {
					parameters = Decompiler.DnlibExtensions.GetParameters((PropertyDef)member).ToList();
				}
				else if (member.IsMethod) {
					var mr = (IMethod)member;
					if (mr.NumberOfGenericParameters > 0) {
						b.Append("``");
						b.Append(mr.NumberOfGenericParameters);
					}
					parameters = Decompiler.DnlibExtensions.GetParameters(mr);
					if (mr.Name == "op_Implicit" || mr.Name == "op_Explicit") {
						explicitReturnType = mr.MethodSig.GetRetType();
					}
				}
				else {
					parameters = null;
				}
				if (parameters != null && Decompiler.DnlibExtensions.HasNormalParameter(parameters)) {
					b.Append('(');
					for (int i = 0; i < parameters.Count; i++) {
						var param = parameters[i];
						if (!param.IsNormalMethodParameter)
							continue;
						if (param.MethodSigIndex > 0)
							b.Append(',');
						AppendTypeName(b, param.Type);
					}
					b.Append(')');
				}
				if (explicitReturnType != null) {
					b.Append('~');
					AppendTypeName(b, explicitReturnType);
				}
			}
			return b.ToString();
		}

		static void AppendTypeName(StringBuilder b, TypeSig type) {
			if (type == null) {
				// could happen when a TypeSpecification has no ElementType; e.g. function pointers in C++/CLI assemblies
				return;
			}
			if (type is GenericInstSig) {
				GenericInstSig giType = (GenericInstSig)type;
				AppendTypeNameWithArguments(b, giType.GenericType == null ? null : giType.GenericType.TypeDefOrRef, giType.GenericArguments);
				return;
			}
			ArraySigBase arrayType = type as ArraySigBase;
			if (arrayType != null) {
				AppendTypeName(b, arrayType.Next);
				b.Append('[');
				var lowerBounds = arrayType.GetLowerBounds();
				var sizes = arrayType.GetSizes();
				for (int i = 0; i < arrayType.Rank; i++) {
					if (i > 0)
						b.Append(',');
					if (i < lowerBounds.Count && i < sizes.Count) {
						b.Append(lowerBounds[i]);
						b.Append(':');
						b.Append(sizes[i] + lowerBounds[i] - 1);
					}
				}
				b.Append(']');
				return;
			}
			ByRefSig refType = type as ByRefSig;
			if (refType != null) {
				AppendTypeName(b, refType.Next);
				b.Append('@');
				return;
			}
			PtrSig ptrType = type as PtrSig;
			if (ptrType != null) {
				AppendTypeName(b, ptrType.Next);
				b.Append('*');
				return;
			}
			GenericSig gp = type as GenericSig;
			if (gp != null) {
				b.Append('`');
				if (gp.IsMethodVar) {
					b.Append('`');
				}
				b.Append(gp.Number);
			}
			else {
				var typeRef = type.ToTypeDefOrRef();
				if (typeRef.DeclaringType != null) {
					AppendTypeName(b, typeRef.DeclaringType.ToTypeSig());
					b.Append('.');
					b.Append(typeRef.Name);
				}
				else {
					b.Append(type.FullName);
				}
			}
		}

		static int AppendTypeNameWithArguments(StringBuilder b, ITypeDefOrRef type, IList<TypeSig> genericArguments) {
			if (type == null)
				return 0;
			int outerTypeParameterCount = 0;
			if (type.DeclaringType != null) {
				ITypeDefOrRef declType = type.DeclaringType;
				outerTypeParameterCount = AppendTypeNameWithArguments(b, declType, genericArguments);
				b.Append('.');
			}
			else if (!string.IsNullOrEmpty(type.Namespace)) {
				b.Append(type.Namespace);
				b.Append('.');
			}
			int localTypeParameterCount = 0;
			b.Append(NRefactory.TypeSystem.ReflectionHelper.SplitTypeParameterCountFromReflectionName(type.Name, out localTypeParameterCount));

			if (localTypeParameterCount > 0) {
				int totalTypeParameterCount = outerTypeParameterCount + localTypeParameterCount;
				b.Append('{');
				for (int i = outerTypeParameterCount; i < totalTypeParameterCount && i < genericArguments.Count; i++) {
					if (i > outerTypeParameterCount)
						b.Append(',');
					AppendTypeName(b, genericArguments[i]);
				}
				b.Append('}');
			}
			return outerTypeParameterCount + localTypeParameterCount;
		}
		#endregion

		#region FindMemberByKey
		public static IMemberRef FindMemberByKey(ModuleDef module, string key) {
			if (module == null)
				throw new ArgumentNullException("module");
			if (key == null || key.Length < 2 || key[1] != ':')
				return null;
			switch (key[0]) {
			case 'T':
				return FindType(module, key.Substring(2));
			case 'F':
				return FindMember(module, key, type => type.Fields);
			case 'P':
				return FindMember(module, key, type => type.Properties);
			case 'E':
				return FindMember(module, key, type => type.Events);
			case 'M':
				return FindMember(module, key, type => type.Methods);
			default:
				return null;
			}
		}

		static IMemberRef FindMember(ModuleDef module, string key, Func<TypeDef, IEnumerable<IMemberRef>> memberSelector) {
			Debug.WriteLine("Looking for member " + key);
			int parenPos = key.IndexOf('(');
			int dotPos;
			if (parenPos > 0) {
				dotPos = key.LastIndexOf('.', parenPos - 1, parenPos);
			}
			else {
				dotPos = key.LastIndexOf('.');
			}
			if (dotPos < 0)
				return null;
			TypeDef type = FindType(module, key.Substring(2, dotPos - 2));
			if (type == null)
				return null;
			string shortName;
			if (parenPos > 0) {
				shortName = key.Substring(dotPos + 1, parenPos - (dotPos + 1));
			}
			else {
				shortName = key.Substring(dotPos + 1);
			}
			Debug.WriteLine("Searching in type {0} for {1}", type.FullName, shortName);
			IMemberRef shortNameMatch = null;
			foreach (IMemberRef member in memberSelector(type)) {
				string memberKey = GetKey(member);
				Debug.WriteLine(memberKey);
				if (memberKey == key)
					return member;
				if (shortName == member.Name.Replace('.', '#'))
					shortNameMatch = member;
			}
			// if there's no match by ID string (key), return the match by name.
			return shortNameMatch;
		}

		static TypeDef FindType(ModuleDef module, string name) {
			int pos = name.LastIndexOf('.');
			if (string.IsNullOrEmpty(name))
				return null;
			TypeDef type = module.Find(name, true);
			if (type == null && pos > 0) { // Original code only entered if ns.Length > 0
										   // try if this is a nested type
				type = FindType(module, name.Substring(0, pos));
				if (type != null) {
					type = type.NestedTypes.FirstOrDefault(t => t.Name == name);
				}
			}
			return type;
		}
		#endregion
	}
}
