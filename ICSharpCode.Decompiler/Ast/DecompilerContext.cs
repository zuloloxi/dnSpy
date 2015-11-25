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
using System.Threading;
using ICSharpCode.Decompiler.Ast;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using dnlib.DotNet;

namespace ICSharpCode.Decompiler
{
	public class DecompilerContext
	{
		public ModuleDef CurrentModule;
		public CancellationToken CancellationToken;
		public TypeDef CurrentType;
		public MethodDef CurrentMethod;
		public DecompilerSettings Settings = new DecompilerSettings();
		public bool CurrentMethodIsAsync;
		
		public static DecompilerContext CreateTestContext(ModuleDef currentModule)
		{
			var ctx = new DecompilerContext(currentModule);
			ctx.Settings.InitializeForTest();
			return ctx;
		}

		public DecompilerContext(ModuleDef currentModule)
		{
			this.CurrentModule = currentModule;
		}
		
		/// <summary>
		/// Used to pass variable names from a method to its anonymous methods.
		/// </summary>
		internal List<string> ReservedVariableNames = new List<string>();
		
		public DecompilerContext Clone()
		{
			DecompilerContext ctx = (DecompilerContext)MemberwiseClone();
			ctx.ReservedVariableNames = new List<string>(ctx.ReservedVariableNames);
			return ctx;
		}
	}
}
