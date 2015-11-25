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
using System.Linq;
using System.Threading;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnSpy.NRefactory;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Ast;

namespace ICSharpCode.ILSpy.TreeNodes.Analyzer {
	internal sealed class AnalyzedTypeInstantiationsTreeNode : AnalyzerSearchTreeNode {
		private readonly TypeDef analyzedType;
		private readonly bool isSystemObject;

		public AnalyzedTypeInstantiationsTreeNode(TypeDef analyzedType) {
			if (analyzedType == null)
				throw new ArgumentNullException("analyzedType");

			this.analyzedType = analyzedType;

			this.isSystemObject = analyzedType.DefinitionAssembly.IsCorLib() && analyzedType.FullName == "System.Object";
		}

		protected override void Write(ITextOutput output, Language language) {
			output.Write("Instantiated By", TextTokenType.Text);
		}

		protected override IEnumerable<AnalyzerTreeNode> FetchChildren(CancellationToken ct) {
			var analyzer = new ScopedWhereUsedAnalyzer<AnalyzerTreeNode>(analyzedType, FindReferencesInType);
			return analyzer.PerformAnalysis(ct).OrderBy(n => n.ToString(Language));
		}

		private IEnumerable<AnalyzerTreeNode> FindReferencesInType(TypeDef type) {
			foreach (MethodDef method in type.Methods) {
				bool found = false;
				if (!method.HasBody)
					continue;

				// ignore chained constructors
				// (since object is the root of everything, we can short circuit the test in this case)
				if (method.Name == ".ctor" &&
					(isSystemObject || analyzedType == type || TypesHierarchyHelpers.IsBaseType(analyzedType, type, false)))
					continue;

				foreach (Instruction instr in method.Body.Instructions) {
					IMethod mr = instr.Operand as IMethod;
					if (mr != null && !mr.IsField && mr.Name == ".ctor") {
						if (Helpers.IsReferencedBy(analyzedType, mr.DeclaringType)) {
							found = true;
							break;
						}
					}
				}

				Helpers.FreeMethodBody(method);

				if (found) {
					var node = new AnalyzedMethodTreeNode(method);
					node.Language = this.Language;
					yield return node;
				}
			}
		}

		public static bool CanShow(TypeDef type) {
			return (type.IsClass && !(type.IsAbstract && type.IsSealed) && !type.IsEnum);
		}
	}
}
