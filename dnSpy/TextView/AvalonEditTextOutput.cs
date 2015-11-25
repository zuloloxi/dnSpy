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
using System.Windows;
using dnSpy.NRefactory;
using dnSpy.TextView;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.Decompiler;

namespace ICSharpCode.ILSpy.TextView {
	/// <summary>
	/// A text segment that references some object. Used for hyperlinks in the editor.
	/// </summary>
	public sealed class ReferenceSegment : TextSegment {
		public object Reference;
		public bool IsLocal;
		public bool IsLocalTarget;
	}

	/// <summary>
	/// Stores the positions of the definitions that were written to the text output.
	/// </summary>
	sealed class DefinitionLookup {
		internal Dictionary<object, int> definitions = new Dictionary<object, int>();

		public int GetDefinitionPosition(object definition) {
			int val;
			if (definition != null && definitions.TryGetValue(definition, out val))
				return val;
			else
				return -1;
		}

		public void AddDefinition(object definition, int offset) {
			if (definition != null)
				definitions[definition] = offset;
		}
	}

	/// <summary>
	/// Text output implementation for AvalonEdit.
	/// </summary>
	public sealed class AvalonEditTextOutput : ISmartTextOutput {
		public bool CanBeCached {
			get { return canBeCached; }
			private set { canBeCached = value; }
		}
		bool canBeCached = true;
		internal LanguageTokens LanguageTokens {
			get { return tokens; }
		}
		LanguageTokens tokens = new LanguageTokens();
		int lastLineStart = 0;
		int lineNumber = 1;
		readonly StringBuilder b = new StringBuilder();

		/// <summary>Current indentation level</summary>
		int indent;
		/// <summary>Whether indentation should be inserted on the next write</summary>
		bool needsIndent;

		internal readonly List<VisualLineElementGenerator> elementGenerators = new List<VisualLineElementGenerator>();

		/// <summary>List of all references that were written to the output</summary>
		TextSegmentCollection<ReferenceSegment> references = new TextSegmentCollection<ReferenceSegment>();

		internal readonly DefinitionLookup DefinitionLookup = new DefinitionLookup();

		/// <summary>Embedded UIElements, see <see cref="UIElementGenerator"/>.</summary>
		internal readonly List<KeyValuePair<int, Lazy<UIElement>>> UIElements = new List<KeyValuePair<int, Lazy<UIElement>>>();

		internal readonly List<MemberMapping> DebuggerMemberMappings = new List<MemberMapping>();

		public AvalonEditTextOutput() {
		}

		/// <summary>
		/// Gets the list of references (hyperlinks).
		/// </summary>
		internal TextSegmentCollection<ReferenceSegment> References {
			get { return references; }
		}

		public void AddVisualLineElementGenerator(VisualLineElementGenerator elementGenerator) {
			elementGenerators.Add(elementGenerator);
		}

		/// <summary>
		/// Controls the maximum length of the text.
		/// When this length is exceeded, an <see cref="OutputLengthExceededException"/> will be thrown,
		/// thus aborting the decompilation.
		/// </summary>
		public int LengthLimit = int.MaxValue;

		public int TextLength {
			get { return b.Length; }
		}

		internal string Text {
			get { return b.ToString(); }
		}

		public ICSharpCode.NRefactory.TextLocation Location {
			get {
				return new ICSharpCode.NRefactory.TextLocation(lineNumber, b.Length - lastLineStart + 1 + (needsIndent ? indent : 0));
			}
		}

		#region Text Document
		TextDocument textDocument;

		/// <summary>
		/// Prepares the TextDocument.
		/// This method may be called by the background thread writing to the output.
		/// Once the document is prepared, it can no longer be written to.
		/// </summary>
		/// <remarks>
		/// Calling this method on the background thread ensures the TextDocument's line tokenization
		/// runs in the background and does not block the GUI.
		/// </remarks>
		public void PrepareDocument() {
			if (textDocument == null) {
				textDocument = new TextDocument(b.ToString());
				textDocument.SetOwnerThread(null); // release ownership
			}
		}

		/// <summary>
		/// Retrieves the TextDocument.
		/// Once the document is retrieved, it can no longer be written to.
		/// </summary>
		public TextDocument GetDocument() {
			PrepareDocument();
			textDocument.SetOwnerThread(System.Threading.Thread.CurrentThread); // acquire ownership
			return textDocument;
		}
		#endregion

		public void Indent() {
			indent++;
		}

		public void Unindent() {
			indent--;
		}

		void WriteIndent() {
			Debug.Assert(textDocument == null);
			if (needsIndent) {
				needsIndent = false;
				for (int i = 0; i < indent; i++) {
					Append(TextTokenType.Text, '\t');
				}
			}
		}

		public void Write(char ch, TextTokenType tokenType) {
			WriteIndent();
			Append(tokenType, ch);
		}

		public void Write(string text, TextTokenType tokenType) {
			WriteIndent();
			Append(tokenType, text);
		}

		public void WriteLine() {
			Debug.Assert(textDocument == null);
			AppendLine();
			needsIndent = true;
			lastLineStart = b.Length;
			lineNumber++;
			if (this.TextLength > LengthLimit) {
				throw new OutputLengthExceededException();
			}
		}

		public void WriteDefinition(string text, object definition, TextTokenType tokenType, bool isLocal) {
			WriteIndent();
			int start = this.TextLength;
			Append(tokenType, text);
			int end = this.TextLength;
			this.DefinitionLookup.AddDefinition(definition, this.TextLength);
			references.Add(new ReferenceSegment { StartOffset = start, EndOffset = end, Reference = definition, IsLocal = isLocal, IsLocalTarget = true });
		}

		public void WriteReference(string text, object reference, TextTokenType tokenType, bool isLocal) {
			WriteIndent();
			int start = this.TextLength;
			Append(tokenType, text);
			int end = this.TextLength;
			references.Add(new ReferenceSegment { StartOffset = start, EndOffset = end, Reference = reference, IsLocal = isLocal });
		}

		public void MarkFoldStart(string collapsedText, bool defaultCollapsed) {
		}

		public void MarkFoldEnd() {
		}

		public void AddUIElement(Func<UIElement> element) {
			if (element != null) {
				if (this.UIElements.Count > 0 && this.UIElements.Last().Key == this.TextLength)
					throw new InvalidOperationException("Only one UIElement is allowed for each position in the document");
				this.UIElements.Add(new KeyValuePair<int, Lazy<UIElement>>(this.TextLength, new Lazy<UIElement>(element)));
				MarkAsNonCached();
			}
		}

		public void AddDebugSymbols(MemberMapping methodDebugSymbols) {
			DebuggerMemberMappings.Add(methodDebugSymbols);
		}

		void Append(TextTokenType tokenType, char c) {
			tokens.Append(tokenType, c);
			b.Append(c);
			Debug.Assert(b.Length == tokens.Length);
		}

		void Append(TextTokenType tokenType, string s) {
			tokens.Append(tokenType, s);
			b.Append(s);
			Debug.Assert(b.Length == tokens.Length);
		}

		void AppendLine() {
			tokens.AppendLine();
			b.AppendLine();
			Debug.Assert(b.Length == tokens.Length);
		}

		public void MarkAsNonCached() {
			CanBeCached = false;
		}
	}
}
