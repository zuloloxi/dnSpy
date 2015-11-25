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
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ICSharpCode.NRefactory.CSharp;

namespace ICSharpCode.Decompiler
{
	public enum DecompilationObject
	{
		NestedTypes,
		Fields,
		Events,
		Properties,
		Methods,
	}

	/// <summary>
	/// Settings for the decompiler.
	/// </summary>
	public class DecompilerSettings : INotifyPropertyChanged, IEquatable<DecompilerSettings>
	{
		public DecompilerSettings InitializeForTest()
		{
			ShowILComments = false;
			RemoveEmptyDefaultConstructors = true;
			ShowTokenAndRvaComments = false;
			ShowILBytes = false;
			SortMembers = false;
			ForceShowAllMembers = false;
			SortSystemUsingStatementsFirst = false;
			DecompilationObject0 = DecompilationObject.NestedTypes;
			DecompilationObject1 = DecompilationObject.Fields;
			DecompilationObject2 = DecompilationObject.Events;
			DecompilationObject3 = DecompilationObject.Properties;
			DecompilationObject4 = DecompilationObject.Methods;
			return this;
		}

		DecompilationObject[] decompilationObjects = new DecompilationObject[5] {
			DecompilationObject.Methods,
			DecompilationObject.Properties,
			DecompilationObject.Events,
			DecompilationObject.Fields,
			DecompilationObject.NestedTypes,
		};

		public IEnumerable<DecompilationObject> DecompilationObjects {
			get { return decompilationObjects.AsEnumerable(); }
		}

		public DecompilationObject[] DecompilationObjectsArray {
			get { return typeof(DecompilationObject).GetEnumValues().Cast<DecompilationObject>().ToArray(); }
		}

		public DecompilationObject DecompilationObject0 {
			get { return decompilationObjects[0]; }
			set { SetDecompilationObject(0, value); }
		}

		public DecompilationObject DecompilationObject1 {
			get { return decompilationObjects[1]; }
			set { SetDecompilationObject(1, value); }
		}

		public DecompilationObject DecompilationObject2 {
			get { return decompilationObjects[2]; }
			set { SetDecompilationObject(2, value); }
		}

		public DecompilationObject DecompilationObject3 {
			get { return decompilationObjects[3]; }
			set { SetDecompilationObject(3, value); }
		}

		public DecompilationObject DecompilationObject4 {
			get { return decompilationObjects[4]; }
			set { SetDecompilationObject(4, value); }
		}

		void SetDecompilationObject(int index, DecompilationObject newValue)
		{
			if (decompilationObjects[index] == newValue)
				return;

			int otherIndex = Array.IndexOf(decompilationObjects, newValue);
			Debug.Assert(otherIndex >= 0);
			if (otherIndex >= 0) {
				decompilationObjects[otherIndex] = decompilationObjects[index];
				decompilationObjects[index] = newValue;

				OnPropertyChanged(string.Format("DecompilationObject{0}", otherIndex));
			}
			OnPropertyChanged(string.Format("DecompilationObject{0}", index));
		}

		bool anonymousMethods = true;
		
		/// <summary>
		/// Decompile anonymous methods/lambdas.
		/// </summary>
		public bool AnonymousMethods {
			get { return anonymousMethods; }
			set {
				if (anonymousMethods != value) {
					anonymousMethods = value;
					OnPropertyChanged("AnonymousMethods");
				}
			}
		}
		
		bool expressionTrees = true;
		
		/// <summary>
		/// Decompile expression trees.
		/// </summary>
		public bool ExpressionTrees {
			get { return expressionTrees; }
			set {
				if (expressionTrees != value) {
					expressionTrees = value;
					OnPropertyChanged("ExpressionTrees");
				}
			}
		}
		
		bool yieldReturn = true;
		
		/// <summary>
		/// Decompile enumerators.
		/// </summary>
		public bool YieldReturn {
			get { return yieldReturn; }
			set {
				if (yieldReturn != value) {
					yieldReturn = value;
					OnPropertyChanged("YieldReturn");
				}
			}
		}
		
		bool asyncAwait = true;
		
		/// <summary>
		/// Decompile async methods.
		/// </summary>
		public bool AsyncAwait {
			get { return asyncAwait; }
			set {
				if (asyncAwait != value) {
					asyncAwait = value;
					OnPropertyChanged("AsyncAwait");
				}
			}
		}
		
		bool automaticProperties = true;
		
		/// <summary>
		/// Decompile automatic properties
		/// </summary>
		public bool AutomaticProperties {
			get { return automaticProperties; }
			set {
				if (automaticProperties != value) {
					automaticProperties = value;
					OnPropertyChanged("AutomaticProperties");
				}
			}
		}
		
		bool automaticEvents = true;
		
		/// <summary>
		/// Decompile automatic events
		/// </summary>
		public bool AutomaticEvents {
			get { return automaticEvents; }
			set {
				if (automaticEvents != value) {
					automaticEvents = value;
					OnPropertyChanged("AutomaticEvents");
				}
			}
		}
		
		bool usingStatement = true;
		
		/// <summary>
		/// Decompile using statements.
		/// </summary>
		public bool UsingStatement {
			get { return usingStatement; }
			set {
				if (usingStatement != value) {
					usingStatement = value;
					OnPropertyChanged("UsingStatement");
				}
			}
		}
		
		bool forEachStatement = true;
		
		/// <summary>
		/// Decompile foreach statements.
		/// </summary>
		public bool ForEachStatement {
			get { return forEachStatement; }
			set {
				if (forEachStatement != value) {
					forEachStatement = value;
					OnPropertyChanged("ForEachStatement");
				}
			}
		}
		
		bool lockStatement = true;
		
		/// <summary>
		/// Decompile lock statements.
		/// </summary>
		public bool LockStatement {
			get { return lockStatement; }
			set {
				if (lockStatement != value) {
					lockStatement = value;
					OnPropertyChanged("LockStatement");
				}
			}
		}
		
		bool switchStatementOnString = true;
		
		public bool SwitchStatementOnString {
			get { return switchStatementOnString; }
			set {
				if (switchStatementOnString != value) {
					switchStatementOnString = value;
					OnPropertyChanged("SwitchStatementOnString");
				}
			}
		}
		
		bool usingDeclarations = true;
		
		public bool UsingDeclarations {
			get { return usingDeclarations; }
			set {
				if (usingDeclarations != value) {
					usingDeclarations = value;
					OnPropertyChanged("UsingDeclarations");
				}
			}
		}
		
		bool queryExpressions = true;
		
		public bool QueryExpressions {
			get { return queryExpressions; }
			set {
				if (queryExpressions != value) {
					queryExpressions = value;
					OnPropertyChanged("QueryExpressions");
				}
			}
		}
		
		bool fullyQualifyAmbiguousTypeNames = true;
		
		public bool FullyQualifyAmbiguousTypeNames {
			get { return fullyQualifyAmbiguousTypeNames; }
			set {
				if (fullyQualifyAmbiguousTypeNames != value) {
					fullyQualifyAmbiguousTypeNames = value;
					OnPropertyChanged("FullyQualifyAmbiguousTypeNames");
				}
			}
		}
		
		bool useDebugSymbols = true;
		
		/// <summary>
		/// Gets/Sets whether to use variable names from debug symbols, if available.
		/// </summary>
		public bool UseDebugSymbols {
			get { return useDebugSymbols; }
			set {
				if (useDebugSymbols != value) {
					useDebugSymbols = value;
					OnPropertyChanged("UseDebugSymbols");
				}
			}
		}
		
		bool objectCollectionInitializers = true;
		
		/// <summary>
		/// Gets/Sets whether to use C# 3.0 object/collection initializers
		/// </summary>
		public bool ObjectOrCollectionInitializers {
			get { return objectCollectionInitializers; }
			set {
				if (objectCollectionInitializers != value) {
					objectCollectionInitializers = value;
					OnPropertyChanged("ObjectCollectionInitializers");
				}
			}
		}
		
		bool showXmlDocumentation = true;
		
		/// <summary>
		/// Gets/Sets whether to include XML documentation comments in the decompiled code
		/// </summary>
		public bool ShowXmlDocumentation {
			get { return showXmlDocumentation; }
			set {
				if (showXmlDocumentation != value) {
					showXmlDocumentation = value;
					OnPropertyChanged("ShowXmlDocumentation");
				}
			}
		}

		bool showILComments = false;

		public bool ShowILComments {
			get { return showILComments; }
			set {
				if (showILComments != value) {
					showILComments = value;
					OnPropertyChanged("ShowILComments");
				}
			}
		}

		bool removeEmptyDefaultConstructors = true;

		public bool RemoveEmptyDefaultConstructors {
			get { return removeEmptyDefaultConstructors; }
			set {
				if (removeEmptyDefaultConstructors != value) {
					removeEmptyDefaultConstructors = value;
					OnPropertyChanged("RemoveEmptyDefaultConstructors");
				}
			}
		}
		
		#region Options to aid VB decompilation
		bool introduceIncrementAndDecrement = true;
		
		/// <summary>
		/// Gets/Sets whether to use increment and decrement operators
		/// </summary>
		public bool IntroduceIncrementAndDecrement {
			get { return introduceIncrementAndDecrement; }
			set {
				if (introduceIncrementAndDecrement != value) {
					introduceIncrementAndDecrement = value;
					OnPropertyChanged("IntroduceIncrementAndDecrement");
				}
			}
		}
		
		bool makeAssignmentExpressions = true;
		
		/// <summary>
		/// Gets/Sets whether to use assignment expressions such as in while ((count = Do()) != 0) ;
		/// </summary>
		public bool MakeAssignmentExpressions {
			get { return makeAssignmentExpressions; }
			set {
				if (makeAssignmentExpressions != value) {
					makeAssignmentExpressions = value;
					OnPropertyChanged("MakeAssignmentExpressions");
				}
			}
		}
		
		bool alwaysGenerateExceptionVariableForCatchBlocks = false;
		
		/// <summary>
		/// Gets/Sets whether to always generate exception variables in catch blocks
		/// </summary>
		public bool AlwaysGenerateExceptionVariableForCatchBlocks {
			get { return alwaysGenerateExceptionVariableForCatchBlocks; }
			set {
				if (alwaysGenerateExceptionVariableForCatchBlocks != value) {
					alwaysGenerateExceptionVariableForCatchBlocks = value;
					OnPropertyChanged("AlwaysGenerateExceptionVariableForCatchBlocks");
				}
			}
		}
		#endregion

		bool showTokenAndRvaComments = true;

		/// <summary>
		/// Gets/sets whether to show tokens of types/methods/etc and the RVA / file offset in comments
		/// </summary>
		public bool ShowTokenAndRvaComments {
			get { return showTokenAndRvaComments; }
			set {
				if (showTokenAndRvaComments != value) {
					showTokenAndRvaComments = value;
					OnPropertyChanged("ShowTokenAndRvaComments");
				}
			}
		}

		bool showILBytes = true;

		/// <summary>
		/// Gets/sets whether to show IL instruction bytes
		/// </summary>
		public bool ShowILBytes {
			get { return showILBytes; }
			set {
				if (showILBytes != value) {
					showILBytes = value;
					OnPropertyChanged("ShowILBytes");
				}
			}
		}

		bool sortMembers = true;

		/// <summary>
		/// Gets/sets whether to sort members
		/// </summary>
		public bool SortMembers {
			get { return sortMembers; }
			set {
				if (sortMembers != value) {
					sortMembers = value;
					OnPropertyChanged("SortMembers");
				}
			}
		}

		public bool ForceShowAllMembers {
			get { return forceShowAllMembers; }
			set {
				if (forceShowAllMembers != value) {
					forceShowAllMembers = value;
					OnPropertyChanged("ForceShowAllMembers");
				}
			}
		}
		bool forceShowAllMembers = false;

		public bool SortSystemUsingStatementsFirst {
			get { return sortSystemUsingStatementsFirst; }
			set {
				if (sortSystemUsingStatementsFirst != value) {
					sortSystemUsingStatementsFirst = value;
					OnPropertyChanged("SortSystemUsingStatementsFirst");
				}
			}
		}
		bool sortSystemUsingStatementsFirst = true;

		CSharpFormattingOptions csharpFormattingOptions;
		
		public CSharpFormattingOptions CSharpFormattingOptions {
			get {
				if (csharpFormattingOptions == null) {
					csharpFormattingOptions = FormattingOptionsFactory.CreateAllman();
					csharpFormattingOptions.IndentSwitchBody = false;
					csharpFormattingOptions.ArrayInitializerWrapping = Wrapping.WrapAlways;
				}
				return csharpFormattingOptions;
			}
			set {
				if (value == null)
					throw new ArgumentNullException();
				if (csharpFormattingOptions != value) {
					csharpFormattingOptions = value;
					OnPropertyChanged("CSharpFormattingOptions");
				}
			}
		}
		
		public event PropertyChangedEventHandler PropertyChanged;
		
		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null) {
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		
		public DecompilerSettings Clone()
		{
			DecompilerSettings settings = (DecompilerSettings)MemberwiseClone();
			if (csharpFormattingOptions != null)
				settings.csharpFormattingOptions = csharpFormattingOptions.Clone();
			settings.decompilationObjects = (DecompilationObject[])decompilationObjects.Clone();
			settings.PropertyChanged = null;
			return settings;
		}

		public bool Equals(DecompilerSettings other)
		{
			if (other == null)
				return false;

			if (AnonymousMethods != other.AnonymousMethods) return false;
			if (ExpressionTrees != other.ExpressionTrees) return false;
			if (YieldReturn != other.YieldReturn) return false;
			if (AsyncAwait != other.AsyncAwait) return false;
			if (AutomaticProperties != other.AutomaticProperties) return false;
			if (AutomaticEvents != other.AutomaticEvents) return false;
			if (UsingStatement != other.UsingStatement) return false;
			if (ForEachStatement != other.ForEachStatement) return false;
			if (LockStatement != other.LockStatement) return false;
			if (SwitchStatementOnString != other.SwitchStatementOnString) return false;
			if (UsingDeclarations != other.UsingDeclarations) return false;
			if (QueryExpressions != other.QueryExpressions) return false;
			if (FullyQualifyAmbiguousTypeNames != other.FullyQualifyAmbiguousTypeNames) return false;
			if (UseDebugSymbols != other.UseDebugSymbols) return false;
			if (ObjectOrCollectionInitializers != other.ObjectOrCollectionInitializers) return false;
			if (ShowXmlDocumentation != other.ShowXmlDocumentation) return false;
			if (ShowILComments != other.ShowILComments) return false;
			if (RemoveEmptyDefaultConstructors != other.RemoveEmptyDefaultConstructors) return false;
			if (IntroduceIncrementAndDecrement != other.IntroduceIncrementAndDecrement) return false;
			if (MakeAssignmentExpressions != other.MakeAssignmentExpressions) return false;
			if (AlwaysGenerateExceptionVariableForCatchBlocks != other.AlwaysGenerateExceptionVariableForCatchBlocks) return false;
			if (ShowTokenAndRvaComments != other.ShowTokenAndRvaComments) return false;
			if (ShowILBytes != other.ShowILBytes) return false;
			if (DecompilationObject0 != other.DecompilationObject0) return false;
			if (DecompilationObject1 != other.DecompilationObject1) return false;
			if (DecompilationObject2 != other.DecompilationObject2) return false;
			if (DecompilationObject3 != other.DecompilationObject3) return false;
			if (DecompilationObject4 != other.DecompilationObject4) return false;
			if (SortMembers != other.SortMembers) return false;
			if (ForceShowAllMembers != other.ForceShowAllMembers) return false;
			if (SortSystemUsingStatementsFirst != other.SortSystemUsingStatementsFirst) return false;

			//TODO: CSharpFormattingOptions. This isn't currently used but it has a ton of properties

			return true;
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as DecompilerSettings);
		}

		public override int GetHashCode()
		{
			unchecked {
				uint h = 0;

				h ^= AnonymousMethods				? 0 : 0x80000000U;
				h ^= ExpressionTrees				? 0 : 0x40000000U;
				h ^= YieldReturn					? 0 : 0x20000000U;
				h ^= AsyncAwait						? 0 : 0x10000000U;
				h ^= AutomaticProperties			? 0 : 0x08000000U;
				h ^= AutomaticEvents				? 0 : 0x04000000U;
				h ^= UsingStatement					? 0 : 0x02000000U;
				h ^= ForEachStatement				? 0 : 0x01000000U;
				h ^= LockStatement					? 0 : 0x00800000U;
				h ^= SwitchStatementOnString		? 0 : 0x00400000U;
				h ^= UsingDeclarations				? 0 : 0x00200000U;
				h ^= QueryExpressions				? 0 : 0x00100000U;
				h ^= FullyQualifyAmbiguousTypeNames	? 0 : 0x00080000U;
				h ^= UseDebugSymbols				? 0 : 0x00040000U;
				h ^= ObjectOrCollectionInitializers	? 0 : 0x00020000U;
				h ^= ShowXmlDocumentation			? 0 : 0x00010000U;
				h ^= ShowILComments					? 0 : 0x00008000U;
				h ^= IntroduceIncrementAndDecrement	? 0 : 0x00004000U;
				h ^= MakeAssignmentExpressions		? 0 : 0x00002000U;
				h ^= AlwaysGenerateExceptionVariableForCatchBlocks ? 0 : 0x00001000U;
				h ^= RemoveEmptyDefaultConstructors	? 0 : 0x00000800U;
				h ^= ShowTokenAndRvaComments		? 0 : 0x00000400U;
				h ^= ShowILBytes					? 0 : 0x00000200U;
				h ^= SortMembers					? 0 : 0x00000100U;
				h ^= ForceShowAllMembers			? 0 : 0x00000080U;
				h ^= SortSystemUsingStatementsFirst	? 0 : 0x00000040U;

				for (int i = 0; i < decompilationObjects.Length; i++)
					h ^= (uint)decompilationObjects[i] << (i * 8);

				//TODO: CSharpFormattingOptions. This isn't currently used but it has a ton of properties

				return (int)h;
			}
		}
	}
}
