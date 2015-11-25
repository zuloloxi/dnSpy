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
using dnSpy.NRefactory;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Ast;

namespace ICSharpCode.ILSpy.TreeNodes.Analyzer {
	internal sealed class AnalyzedInterfacePropertyImplementedByTreeNode : AnalyzerSearchTreeNode {
		private readonly PropertyDef analyzedProperty;
		private readonly MethodDef analyzedMethod;

		public AnalyzedInterfacePropertyImplementedByTreeNode(PropertyDef analyzedProperty) {
			if (analyzedProperty == null)
				throw new ArgumentNullException("analyzedProperty");

			this.analyzedProperty = analyzedProperty;
			this.analyzedMethod = this.analyzedProperty.GetMethod ?? this.analyzedProperty.SetMethod;
		}

		protected override void Write(ITextOutput output, Language language) {
			output.Write("Implemented By", TextTokenType.Text);
		}

		protected override IEnumerable<AnalyzerTreeNode> FetchChildren(CancellationToken ct) {
			if (analyzedMethod == null)
				return new List<AnalyzerTreeNode>();
			var analyzer = new ScopedWhereUsedAnalyzer<AnalyzerTreeNode>(analyzedMethod, FindReferencesInType);
			return analyzer.PerformAnalysis(ct).OrderBy(n => n.ToString(Language));
		}

		private IEnumerable<AnalyzerTreeNode> FindReferencesInType(TypeDef type) {
			if (analyzedMethod == null)
				yield break;
			if (!type.HasInterfaces)
				yield break;
			var iff = type.Interfaces.FirstOrDefault(i => new SigComparer().Equals(i.Interface, analyzedMethod.DeclaringType));
			ITypeDefOrRef implementedInterfaceRef = iff == null ? null : iff.Interface;
			if (implementedInterfaceRef == null)
				yield break;

			//TODO: Can we compare property sigs too?
			foreach (PropertyDef property in type.Properties.Where(e => e.Name == analyzedProperty.Name)) {
				MethodDef accessor = property.GetMethod ?? property.SetMethod;
				if (accessor != null && TypesHierarchyHelpers.MatchInterfaceMethod(accessor, analyzedMethod, implementedInterfaceRef)) {
					var node = new AnalyzedPropertyTreeNode(property);
					node.Language = this.Language;
					yield return node;
				}
				yield break;
			}

			foreach (PropertyDef property in type.Properties.Where(e => e.Name.EndsWith(analyzedProperty.Name))) {
				MethodDef accessor = property.GetMethod ?? property.SetMethod;
				if (accessor != null && accessor.HasOverrides && accessor.Overrides.Any(m => new SigComparer(SigComparerOptions.CompareDeclaringTypes | SigComparerOptions.PrivateScopeIsComparable).Equals(m.MethodDeclaration, analyzedMethod))) {
					var node = new AnalyzedPropertyTreeNode(property);
					node.Language = this.Language;
					yield return node;
				}
			}
		}

		public static bool CanShow(PropertyDef property) {
			return property.DeclaringType.IsInterface;
		}
	}
}
