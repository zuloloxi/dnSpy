﻿/*
	Copyright (c) 2015 Ki

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in
	all copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
	THE SOFTWARE.
*/

using System.Xml;
using System.Xml.Linq;
using dnlib.DotNet;

namespace dnSpy.BamlDecompiler.Xaml {
	internal class XamlType {
		public IAssembly Assembly { get; private set; }
		public string TypeNamespace { get; private set; }
		public string TypeName { get; private set; }

		public XNamespace Namespace { get; private set; }
		public ITypeDefOrRef ResolvedType { get; set; }

		public XamlType(IAssembly assembly, string ns, string name)
			: this(assembly, ns, name, null) {
		}

		public XamlType(IAssembly assembly, string ns, string name, XNamespace xmlns) {
			Assembly = assembly;
			TypeNamespace = ns;
			TypeName = name;
			Namespace = xmlns;
		}

		public void ResolveNamespace(XElement elem, XamlContext ctx) {
			if (Namespace != null)
				return;

			// Since XmlnsProperty records are inside the element,
			// the namespace is resolved after processing the element body.

			string xmlNs = null;
			if (elem.Annotation<XmlnsScope>() != null)
				xmlNs = elem.Annotation<XmlnsScope>().LookupXmlns(Assembly, TypeNamespace);
			if (xmlNs == null)
				xmlNs = ctx.XmlNs.LookupXmlns(Assembly, TypeNamespace);

			if (xmlNs == null) {
				var nsSeg = TypeNamespace.Split('.');
				var nsName = nsSeg[nsSeg.Length - 1].ToLowerInvariant();
				var prefix = nsName;
				int count = 0;
				while (elem.GetNamespaceOfPrefix(prefix) != null) {
					count++;
					prefix = nsName + count;
				}

				xmlNs = string.Format("clr-namespace:{0};assembly={1}", TypeNamespace, Assembly);
				elem.Add(new XAttribute(XNamespace.Xmlns + XmlConvert.EncodeLocalName(prefix),
					ctx.GetXmlNamespace(xmlNs)));
			}
			Namespace = xmlNs;
		}

		public XName ToXName(XamlContext ctx) {
			if (Namespace == null)
				return XmlConvert.EncodeLocalName(TypeName);
			return Namespace + XmlConvert.EncodeLocalName(TypeName);
		}

		public override string ToString() {
			return TypeName;
		}
	}
}