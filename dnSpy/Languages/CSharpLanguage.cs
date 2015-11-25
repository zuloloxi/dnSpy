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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnSpy.Files;
using dnSpy.MVVM;
using dnSpy.NRefactory;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Ast;
using ICSharpCode.Decompiler.Ast.Transforms;
using ICSharpCode.ILSpy.Options;
using ICSharpCode.ILSpy.XmlDoc;
using ICSharpCode.NRefactory.CSharp;

namespace ICSharpCode.ILSpy {
	/// <summary>
	/// Decompiler logic for C#.
	/// </summary>
	[Export(typeof(Language))]
	public class CSharpLanguage : Language {
		string name = "C#";
		bool showAllMembers = false;
		Predicate<IAstTransform> transformAbortCondition = null;

		public static Dictionary<string, string[]> nameToOperatorName = new Dictionary<string, string[]> {
			{ "op_Addition", "operator +".Split(' ') },
			{ "op_BitwiseAnd", "operator &".Split(' ') },
			{ "op_BitwiseOr", "operator |".Split(' ') },
			{ "op_Decrement", "operator --".Split(' ') },
			{ "op_Division", "operator /".Split(' ') },
			{ "op_Equality", "operator ==".Split(' ') },
			{ "op_ExclusiveOr", "operator ^".Split(' ') },
			{ "op_Explicit", "explicit operator".Split(' ') },
			{ "op_False", "operator false".Split(' ') },
			{ "op_GreaterThan", "operator >".Split(' ') },
			{ "op_GreaterThanOrEqual", "operator >=".Split(' ') },
			{ "op_Implicit", "implicit operator".Split(' ') },
			{ "op_Increment", "operator ++".Split(' ') },
			{ "op_Inequality", "operator !=".Split(' ') },
			{ "op_LeftShift", "operator <<".Split(' ') },
			{ "op_LessThan", "operator <".Split(' ') },
			{ "op_LessThanOrEqual", "operator <=".Split(' ') },
			{ "op_LogicalNot", "operator !".Split(' ') },
			{ "op_Modulus", "operator %".Split(' ') },
			{ "op_Multiply", "operator *".Split(' ') },
			{ "op_OnesComplement", "operator ~".Split(' ') },
			{ "op_RightShift", "operator >>".Split(' ') },
			{ "op_Subtraction", "operator -".Split(' ') },
			{ "op_True", "operator true".Split(' ') },
			{ "op_UnaryNegation", "operator -".Split(' ') },
			{ "op_UnaryPlus", "operator +".Split(' ') },
		};

		public CSharpLanguage() {
		}

#if DEBUG
		internal static IEnumerable<CSharpLanguage> GetDebugLanguages() {
			DecompilerContext context = new DecompilerContext(new ModuleDefUser("dummy"));
			string lastTransformName = "no transforms";
			foreach (Type _transformType in TransformationPipeline.CreatePipeline(context).Select(v => v.GetType()).Distinct()) {
				Type transformType = _transformType; // copy for lambda
				yield return new CSharpLanguage {
					transformAbortCondition = v => transformType.IsInstanceOfType(v),
					name = "C# - " + lastTransformName,
					showAllMembers = true
				};
				lastTransformName = "after " + transformType.Name;
			}
			yield return new CSharpLanguage {
				name = "C# - " + lastTransformName,
				showAllMembers = true
			};
		}
#endif

		public override string Name {
			get { return name; }
		}

		public override string FileExtension {
			get { return ".cs"; }
		}

		public override string ProjectFileExtension {
			get { return ".csproj"; }
		}

		public override void DecompileMethod(MethodDef method, ITextOutput output, DecompilationOptions options) {
			WriteCommentLineDeclaringType(output, method);
			AstBuilder codeDomBuilder = CreateAstBuilder(options, currentType: method.DeclaringType, isSingleMember: true);
			if (method.IsConstructor && !method.IsStatic && !DnlibExtensions.IsValueType(method.DeclaringType)) {
				// also fields and other ctors so that the field initializers can be shown as such
				AddFieldsAndCtors(codeDomBuilder, method.DeclaringType, method.IsStatic);
				RunTransformsAndGenerateCode(codeDomBuilder, output, options, new SelectCtorTransform(method));
			}
			else {
				codeDomBuilder.AddMethod(method);
				RunTransformsAndGenerateCode(codeDomBuilder, output, options);
			}
		}

		class SelectCtorTransform : IAstTransform {
			readonly MethodDef ctorDef;

			public SelectCtorTransform(MethodDef ctorDef) {
				this.ctorDef = ctorDef;
			}

			public void Run(AstNode compilationUnit) {
				ConstructorDeclaration ctorDecl = null;
				foreach (var node in compilationUnit.Children) {
					ConstructorDeclaration ctor = node as ConstructorDeclaration;
					if (ctor != null) {
						if (ctor.Annotation<MethodDef>() == ctorDef) {
							ctorDecl = ctor;
						}
						else {
							// remove other ctors
							ctor.Remove();
						}
					}
					// Remove any fields without initializers
					FieldDeclaration fd = node as FieldDeclaration;
					if (fd != null && fd.Variables.All(v => v.Initializer.IsNull))
						fd.Remove();
				}
				if (ctorDecl.Initializer.ConstructorInitializerType == ConstructorInitializerType.This) {
					// remove all fields
					foreach (var node in compilationUnit.Children)
						if (node is FieldDeclaration)
							node.Remove();
				}
			}
		}

		public override void DecompileProperty(PropertyDef property, ITextOutput output, DecompilationOptions options) {
			WriteCommentLineDeclaringType(output, property);
			AstBuilder codeDomBuilder = CreateAstBuilder(options, currentType: property.DeclaringType, isSingleMember: true);
			codeDomBuilder.AddProperty(property);
			RunTransformsAndGenerateCode(codeDomBuilder, output, options);
		}

		public override void DecompileField(FieldDef field, ITextOutput output, DecompilationOptions options) {
			WriteCommentLineDeclaringType(output, field);
			AstBuilder codeDomBuilder = CreateAstBuilder(options, currentType: field.DeclaringType, isSingleMember: true);
			if (field.IsLiteral) {
				codeDomBuilder.AddField(field);
			}
			else {
				// also decompile ctors so that the field initializer can be shown
				AddFieldsAndCtors(codeDomBuilder, field.DeclaringType, field.IsStatic);
			}
			RunTransformsAndGenerateCode(codeDomBuilder, output, options, new SelectFieldTransform(field));
		}

		/// <summary>
		/// Removes all top-level members except for the specified fields.
		/// </summary>
		sealed class SelectFieldTransform : IAstTransform {
			readonly FieldDef field;

			public SelectFieldTransform(FieldDef field) {
				this.field = field;
			}

			public void Run(AstNode compilationUnit) {
				foreach (var child in compilationUnit.Children) {
					if (child is EntityDeclaration) {
						if (child.Annotation<FieldDef>() != field)
							child.Remove();
					}
				}
			}
		}

		void AddFieldsAndCtors(AstBuilder codeDomBuilder, TypeDef declaringType, bool isStatic) {
			foreach (var field in declaringType.Fields) {
				if (field.IsStatic == isStatic)
					codeDomBuilder.AddField(field);
			}
			foreach (var ctor in declaringType.Methods) {
				if (ctor.IsConstructor && ctor.IsStatic == isStatic)
					codeDomBuilder.AddMethod(ctor);
			}
		}

		public override void DecompileEvent(EventDef ev, ITextOutput output, DecompilationOptions options) {
			WriteCommentLineDeclaringType(output, ev);
			AstBuilder codeDomBuilder = CreateAstBuilder(options, currentType: ev.DeclaringType, isSingleMember: true);
			codeDomBuilder.AddEvent(ev);
			RunTransformsAndGenerateCode(codeDomBuilder, output, options);
		}

		public override void DecompileType(TypeDef type, ITextOutput output, DecompilationOptions options) {
			AstBuilder codeDomBuilder = CreateAstBuilder(options, currentType: type);
			codeDomBuilder.AddType(type);
			RunTransformsAndGenerateCode(codeDomBuilder, output, options);
		}

		void RunTransformsAndGenerateCode(AstBuilder astBuilder, ITextOutput output, DecompilationOptions options, IAstTransform additionalTransform = null) {
			astBuilder.RunTransformations(transformAbortCondition);
			if (additionalTransform != null) {
				additionalTransform.Run(astBuilder.SyntaxTree);
			}
			AddXmlDocumentation(options.DecompilerSettings, astBuilder);
			astBuilder.GenerateCode(output);
		}

		static void AddXmlDocumentation(DecompilerSettings settings, AstBuilder astBuilder) { 
			if (settings.ShowXmlDocumentation) {
				try {
					AddXmlDocTransform.Run(astBuilder.SyntaxTree);
				}
				catch (XmlException ex) {
					string[] msg = (" Exception while reading XmlDoc: " + ex.ToString()).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
					var insertionPoint = astBuilder.SyntaxTree.FirstChild;
					for (int i = 0; i < msg.Length; i++)
						astBuilder.SyntaxTree.InsertChildBefore(insertionPoint, new Comment(msg[i], CommentType.Documentation), Roles.Comment);
				}
			}
		}

		public static string GetPlatformDisplayName(ModuleDef module) {
			switch (module.Machine) {
			case dnlib.PE.Machine.I386:
				if (module.Is32BitPreferred)
					return "AnyCPU (32-bit preferred)";
				else if (module.Is32BitRequired)
					return "x86";
				else
					return "AnyCPU (64-bit preferred)";
			case dnlib.PE.Machine.AMD64:
				return "x64";
			case dnlib.PE.Machine.IA64:
				return "Itanium";
			default:
				return module.Machine.ToString();
			}
		}

		public static string GetPlatformName(ModuleDef module) {
			switch (module.Machine) {
			case dnlib.PE.Machine.I386:
				if (module.Is32BitPreferred)
					return "AnyCPU";
				else if (module.Is32BitRequired)
					return "x86";
				else
					return "AnyCPU";
			case dnlib.PE.Machine.AMD64:
				return "x64";
			case dnlib.PE.Machine.IA64:
				return "Itanium";
			default:
				return module.Machine.ToString();
			}
		}

		public static string GetRuntimeDisplayName(ModuleDef module) {
			if (module.IsClr10)
				return ".NET 1.0";
			if (module.IsClr11)
				return ".NET 1.1";
			if (module.IsClr20)
				return ".NET 2.0";
			if (module.IsClr40)
				return ".NET 4.0";
			return null;
		}

		public override void DecompileAssembly(DnSpyFileList dnSpyFileList, DnSpyFile file, ITextOutput output, DecompilationOptions options, DecompileAssemblyFlags flags = DecompileAssemblyFlags.AssemblyAndModule) {
			if (options.FullDecompilation && options.SaveAsProjectDirectory != null) {
				HashSet<string> directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				var files = WriteCodeFilesInProject(dnSpyFileList, file.ModuleDef, options, directories).ToList();
				files.AddRange(WriteResourceFilesInProject(file, options, directories));
				WriteProjectFile(dnSpyFileList, new TextOutputWriter(output), files, file, options);
			}
			else {
				bool decompileAsm = (flags & DecompileAssemblyFlags.Assembly) != 0;
				bool decompileMod = (flags & DecompileAssemblyFlags.Module) != 0;
				base.DecompileAssembly(dnSpyFileList, file, output, options, flags);
				output.WriteLine();
				ModuleDef mainModule = file.ModuleDef;
				if (decompileMod && mainModule.Types.Count > 0) {
					output.Write("// Global type: ", TextTokenType.Comment);
					output.WriteReference(IdentifierEscaper.Escape(mainModule.GlobalType.FullName), mainModule.GlobalType, TextTokenType.Comment);
					output.WriteLine();
				}
				if (decompileMod || decompileAsm)
					PrintEntryPoint(file, output);
				if (decompileMod) {
					output.WriteLine("// Architecture: " + GetPlatformDisplayName(mainModule), TextTokenType.Comment);
					if (!mainModule.IsILOnly) {
						output.WriteLine("// This assembly contains unmanaged code.", TextTokenType.Comment);
					}
					string runtimeName = GetRuntimeDisplayName(mainModule);
					if (runtimeName != null) {
						output.WriteLine("// Runtime: " + runtimeName, TextTokenType.Comment);
					}
				}
				if (decompileMod || decompileAsm)
					output.WriteLine();

				// don't automatically load additional assemblies when an assembly node is selected in the tree view
				using (options.FullDecompilation ? null : dnSpyFileList.DisableAssemblyLoad()) {
					AstBuilder codeDomBuilder = CreateAstBuilder(options, currentModule: file.ModuleDef);
					codeDomBuilder.AddAssembly(file.ModuleDef, !options.FullDecompilation, decompileAsm, decompileMod);
					codeDomBuilder.RunTransformations(transformAbortCondition);
					codeDomBuilder.GenerateCode(output);
				}
			}
		}

		#region WriteProjectFile
		void WriteProjectFile(DnSpyFileList dnSpyFileList, TextWriter writer, IEnumerable<Tuple<string, string>> files, DnSpyFile assembly, DecompilationOptions options) {
			var module = assembly.ModuleDef;
			const string ns = "http://schemas.microsoft.com/developer/msbuild/2003";
			string platformName = GetPlatformName(module);
			Guid guid = (App.CommandLineArguments == null ? null : App.CommandLineArguments.FixedGuid) ?? Guid.NewGuid();
			using (XmlTextWriter w = new XmlTextWriter(writer)) {
				var asmRefs = GetAssemblyRefs(dnSpyFileList, options, assembly);

				w.Formatting = Formatting.Indented;
				w.WriteStartDocument();
				w.WriteStartElement("Project", ns);
				w.WriteAttributeString("ToolsVersion", "4.0");
				w.WriteAttributeString("DefaultTargets", "Build");

				w.WriteStartElement("PropertyGroup");
				w.WriteElementString("ProjectGuid", (options.ProjectGuid ?? guid).ToString("B").ToUpperInvariant());

				w.WriteStartElement("Configuration");
				w.WriteAttributeString("Condition", " '$(Configuration)' == '' ");
				w.WriteValue("Debug");
				w.WriteEndElement(); // </Configuration>

				w.WriteStartElement("Platform");
				w.WriteAttributeString("Condition", " '$(Platform)' == '' ");
				w.WriteValue(platformName);
				w.WriteEndElement(); // </Platform>

				switch (module.Kind) {
				case ModuleKind.Windows:
					w.WriteElementString("OutputType", "WinExe");
					break;
				case ModuleKind.Console:
					w.WriteElementString("OutputType", "Exe");
					break;
				default:
					w.WriteElementString("OutputType", "Library");
					break;
				}

				if (module.Assembly != null)
					w.WriteElementString("AssemblyName", IdentifierEscaper.Escape(module.Assembly.Name));
				bool useTargetFrameworkAttribute = false;
				var targetFrameworkAttribute = module.Assembly == null ? null : module.Assembly.CustomAttributes.FirstOrDefault(a => a.TypeFullName == "System.Runtime.Versioning.TargetFrameworkAttribute");
				if (targetFrameworkAttribute != null && targetFrameworkAttribute.ConstructorArguments.Any()) {
					string frameworkName = (targetFrameworkAttribute.ConstructorArguments[0].Value as UTF8String) ?? string.Empty;
					string[] frameworkParts = frameworkName.Split(',');
					string frameworkVersion = frameworkParts.FirstOrDefault(a => a.StartsWith("Version="));
					if (frameworkVersion != null) {
						w.WriteElementString("TargetFrameworkVersion", frameworkVersion.Substring("Version=".Length));
						useTargetFrameworkAttribute = true;
					}
					string frameworkProfile = frameworkParts.FirstOrDefault(a => a.StartsWith("Profile="));
					if (frameworkProfile != null)
						w.WriteElementString("TargetFrameworkProfile", frameworkProfile.Substring("Profile=".Length));
				}
				if (!useTargetFrameworkAttribute) {
					if (module.IsClr10) {
						w.WriteElementString("TargetFrameworkVersion", "v1.0");
					}
					else if (module.IsClr11) {
						w.WriteElementString("TargetFrameworkVersion", "v1.1");
					}
					else if (module.IsClr20) {
						w.WriteElementString("TargetFrameworkVersion", "v2.0");
						// TODO: Detect when .NET 3.0/3.5 is required
					}
					else {
						w.WriteElementString("TargetFrameworkVersion", "v4.0");
					}
				}
				w.WriteElementString("WarningLevel", "4");

				w.WriteEndElement(); // </PropertyGroup>

				w.WriteStartElement("PropertyGroup"); // platform-specific
				w.WriteAttributeString("Condition", " '$(Platform)' == '" + platformName + "' ");
				w.WriteElementString("PlatformTarget", platformName);
				w.WriteEndElement(); // </PropertyGroup> (platform-specific)

				w.WriteStartElement("PropertyGroup"); // Debug
				w.WriteAttributeString("Condition", " '$(Configuration)' == 'Debug' ");
				w.WriteElementString("OutputPath", "bin\\Debug\\");
				w.WriteElementString("DebugSymbols", "true");
				w.WriteElementString("DebugType", "full");
				w.WriteElementString("Optimize", "false");
				if (options.DontReferenceStdLib) {
					w.WriteStartElement("NoStdLib");
					w.WriteString("true");
					w.WriteEndElement();
				}
				w.WriteEndElement(); // </PropertyGroup> (Debug)

				w.WriteStartElement("PropertyGroup"); // Release
				w.WriteAttributeString("Condition", " '$(Configuration)' == 'Release' ");
				w.WriteElementString("OutputPath", "bin\\Release\\");
				w.WriteElementString("DebugSymbols", "true");
				w.WriteElementString("DebugType", "pdbonly");
				w.WriteElementString("Optimize", "true");
				if (options.DontReferenceStdLib) {
					w.WriteStartElement("NoStdLib");
					w.WriteString("true");
					w.WriteEndElement();
				}
				w.WriteEndElement(); // </PropertyGroup> (Release)


				w.WriteStartElement("ItemGroup"); // References
				foreach (var r in asmRefs) {
					if (r.Name != "mscorlib") {
						var asm = dnSpyFileList.AssemblyResolver.Resolve(r, module);
						if (asm != null && ExistsInProject(options, asm.Filename))
							continue;
						w.WriteStartElement("Reference");
						w.WriteAttributeString("Include", IdentifierEscaper.Escape(r.Name));
						var hintPath = GetHintPath(options, asm);
						if (hintPath != null) {
							w.WriteStartElement("HintPath");
							w.WriteString(hintPath);
							w.WriteEndElement();
						}
						w.WriteEndElement();
					}
				}
				w.WriteEndElement(); // </ItemGroup> (References)

				foreach (IGrouping<string, string> gr in (from f in files group f.Item2 by f.Item1 into g orderby g.Key select g)) {
					w.WriteStartElement("ItemGroup");
					foreach (string file in gr.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)) {
						w.WriteStartElement(gr.Key);
						w.WriteAttributeString("Include", file);
						w.WriteEndElement();
					}
					w.WriteEndElement();
				}

				w.WriteStartElement("ItemGroup"); // ProjectReference
				foreach (var r in asmRefs) {
					var asm = dnSpyFileList.AssemblyResolver.Resolve(r, module);
					if (asm == null)
						continue;
					var otherProj = FindOtherProject(options, asm.Filename);
					if (otherProj != null) {
						var relPath = GetRelativePath(options.SaveAsProjectDirectory, otherProj.ProjectFileName);
						w.WriteStartElement("ProjectReference");
						w.WriteAttributeString("Include", relPath);
						w.WriteStartElement("Project");
						w.WriteString(otherProj.ProjectGuid.ToString("B").ToUpperInvariant());
						w.WriteEndElement();
						w.WriteStartElement("Name");
						w.WriteString(IdentifierEscaper.Escape(otherProj.AssemblySimpleName));
						w.WriteEndElement();
						w.WriteEndElement();
					}
				}
				w.WriteEndElement(); // </ItemGroup> (ProjectReference)

				w.WriteStartElement("Import");
				w.WriteAttributeString("Project", "$(MSBuildToolsPath)\\Microsoft.CSharp.targets");
				w.WriteEndElement();

				w.WriteEndDocument();
			}
		}

		internal static List<IAssembly> GetAssemblyRefs(DnSpyFileList dnSpyFileList, DecompilationOptions options, DnSpyFile assembly) {
			return new RealAssemblyReferencesFinder(options, assembly).Find(dnSpyFileList);
		}

		class RealAssemblyReferencesFinder {
			readonly DecompilationOptions options;
			readonly DnSpyFile assembly;
			readonly List<IAssembly> allReferences = new List<IAssembly>();
			readonly HashSet<IAssembly> checkedAsms = new HashSet<IAssembly>(AssemblyNameComparer.CompareAll);

			public RealAssemblyReferencesFinder(DecompilationOptions options, DnSpyFile assembly) {
				this.options = options;
				this.assembly = assembly;
			}

			bool ShouldFindRealAsms() {
				return options.ProjectFiles != null && options.DontReferenceStdLib;
			}

			public List<IAssembly> Find(DnSpyFileList dnSpyFileList) {
				if (!ShouldFindRealAsms())
					allReferences.AddRange(assembly.ModuleDef.GetAssemblyRefs());
				else {
					Find(dnSpyFileList, assembly.ModuleDef.CorLibTypes.Object.TypeRef);
					// Some types might've been moved to assembly A and some other types to
					// assembly B. Therefore we must check every type reference and we can't
					// just loop over all asm refs.
					foreach (var tr in assembly.ModuleDef.GetTypeRefs())
						Find(dnSpyFileList, tr);
					for (uint rid = 1; ; rid++) {
						var et = assembly.ModuleDef.ResolveToken(new MDToken(Table.ExportedType, rid)) as ExportedType;
						if (et == null)
							break;
						Find(dnSpyFileList, et);
					}
				}
				return allReferences;
			}

			void Find(DnSpyFileList dnSpyFileList, ExportedType et) {
				if (et == null)
					return;
				// The type might've been moved, so always resolve it instead of using DefinitionAssembly
				var td = et.Resolve(assembly.ModuleDef);
				if (td == null)
					Find(dnSpyFileList, et.DefinitionAssembly);
				else
					Find(dnSpyFileList, td.DefinitionAssembly ?? et.DefinitionAssembly);
			}

			void Find(DnSpyFileList dnSpyFileList, TypeRef typeRef) {
				if (typeRef == null)
					return;
				// The type might've been moved, so always resolve it instead of using DefinitionAssembly
				var td = typeRef.Resolve(assembly.ModuleDef);
				if (td == null)
					Find(dnSpyFileList, typeRef.DefinitionAssembly);
				else
					Find(dnSpyFileList, td.DefinitionAssembly ?? typeRef.DefinitionAssembly);
			}

			void Find(DnSpyFileList dnSpyFileList, IAssembly asmRef) {
				if (asmRef == null)
					return;
				if (checkedAsms.Contains(asmRef))
					return;
				checkedAsms.Add(asmRef);
				var asm = dnSpyFileList.AssemblyResolver.Resolve(asmRef, assembly.ModuleDef);
				if (asm == null)
					allReferences.Add(asmRef);
				else
					AddKnown(asm);
			}

			void AddKnown(DnSpyFile asm) {
				if (asm.Filename.Equals(assembly.Filename, StringComparison.OrdinalIgnoreCase))
					return;
				if (asm.ModuleDef.Assembly != null)
					allReferences.Add(asm.ModuleDef.Assembly);
			}
		}

		internal static string GetHintPath(DecompilationOptions options, DnSpyFile asmRef) {
			if (asmRef == null || options.ProjectFiles == null || options.SaveAsProjectDirectory == null)
				return null;
			if (GacInfo.IsGacPath(asmRef.Filename))
				return null;
			if (ExistsInProject(options, asmRef.Filename))
				return null;

			return GetRelativePath(options.SaveAsProjectDirectory, asmRef.Filename);
		}

		// ("C:\dir1\dir2\dir3", "d:\Dir1\Dir2\Dir3\file.dll") = "d:\Dir1\Dir2\Dir3\file.dll"
		// ("C:\dir1\dir2\dir3", "c:\Dir1\dirA\dirB\file.dll") = "..\..\dirA\dirB\file.dll"
		// ("C:\dir1\dir2\dir3", "c:\Dir1\Dir2\Dir3\Dir4\Dir5\file.dll") = "Dir4\Dir5\file.dll"
		internal static string GetRelativePath(string sourceDir, string destFile) {
			sourceDir = Path.GetFullPath(sourceDir);
			destFile = Path.GetFullPath(destFile);
			if (!Path.GetPathRoot(sourceDir).Equals(Path.GetPathRoot(destFile), StringComparison.OrdinalIgnoreCase))
				return destFile;
			var sourceDirs = GetPathNames(sourceDir);
			var destDirs = GetPathNames(Path.GetDirectoryName(destFile));

			var hintPath = string.Empty;
			int i;
			for (i = 0; i < sourceDirs.Count && i < destDirs.Count; i++) {
				if (!sourceDirs[i].Equals(destDirs[i], StringComparison.OrdinalIgnoreCase))
					break;
			}
			for (int j = i; j < sourceDirs.Count; j++)
				hintPath = Path.Combine(hintPath, "..");
			for (; i < destDirs.Count; i++)
				hintPath = Path.Combine(hintPath, destDirs[i]);
			hintPath = Path.Combine(hintPath, Path.GetFileName(destFile));

			return hintPath;
		}

		static List<string> GetPathNames(string path) {
			var list = new List<string>();
			var root = Path.GetPathRoot(path);
			while (path != root) {
				list.Add(Path.GetFileName(path));
				path = Path.GetDirectoryName(path);
			}
			list.Add(root);
			list.Reverse();
			return list;
		}

		internal static bool ExistsInProject(DecompilationOptions options, string fileName) {
			return FindOtherProject(options, fileName) != null;
		}

		internal static ProjectInfo FindOtherProject(DecompilationOptions options, string fileName) {
			if (options.ProjectFiles == null)
				return null;
			return options.ProjectFiles.FirstOrDefault(f => Path.GetFullPath(f.AssemblyFileName).Equals(Path.GetFullPath(fileName)));
		}
		#endregion

		#region WriteCodeFilesInProject
		bool IncludeTypeWhenDecompilingProject(TypeDef type, DecompilationOptions options) {
			if (type.IsGlobalModuleType || AstBuilder.MemberIsHidden(type, options.DecompilerSettings))
				return false;
			if (type.Namespace == "XamlGeneratedNamespace" && type.Name == "GeneratedInternalTypeHelper")
				return false;
			return true;
		}

		IEnumerable<Tuple<string, string>> WriteAssemblyInfo(DnSpyFileList dnSpyFileList, ModuleDef module, DecompilationOptions options, HashSet<string> directories) {
			// don't automatically load additional assemblies when an assembly node is selected in the tree view
			using (dnSpyFileList.DisableAssemblyLoad()) {
				AstBuilder codeDomBuilder = CreateAstBuilder(options, currentModule: module);
				codeDomBuilder.AddAssembly(module, true, true, true);
				codeDomBuilder.RunTransformations(transformAbortCondition);

				string prop = "Properties";
				if (directories.Add("Properties"))
					Directory.CreateDirectory(Path.Combine(options.SaveAsProjectDirectory, prop));
				string assemblyInfo = Path.Combine(prop, "AssemblyInfo" + this.FileExtension);
				using (StreamWriter w = new StreamWriter(Path.Combine(options.SaveAsProjectDirectory, assemblyInfo)))
					codeDomBuilder.GenerateCode(new PlainTextOutput(w));
				return new Tuple<string, string>[] { Tuple.Create("Compile", assemblyInfo) };
			}
		}

		IEnumerable<Tuple<string, string>> WriteCodeFilesInProject(DnSpyFileList dnSpyFileList, ModuleDef module, DecompilationOptions options, HashSet<string> directories) {
			var files = module.Types.Where(t => IncludeTypeWhenDecompilingProject(t, options)).GroupBy(
				delegate (TypeDef type) {
					string file = TextView.DecompilerTextView.CleanUpName(type.Name) + this.FileExtension;
					if (string.IsNullOrEmpty(type.Namespace)) {
						return file;
					}
					else {
						string dir = TextView.DecompilerTextView.CleanUpName(type.Namespace);
						if (directories.Add(dir))
							Directory.CreateDirectory(Path.Combine(options.SaveAsProjectDirectory, dir));
						return Path.Combine(dir, file);
					}
				}, StringComparer.OrdinalIgnoreCase).ToList();
			AstMethodBodyBuilder.ClearUnhandledOpcodes();
			Parallel.ForEach(
				files,
				new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
				delegate (IGrouping<string, TypeDef> file) {
					using (StreamWriter w = new StreamWriter(Path.Combine(options.SaveAsProjectDirectory, file.Key))) {
						AstBuilder codeDomBuilder = CreateAstBuilder(options, currentModule: module);
						foreach (TypeDef type in file) {
							codeDomBuilder.AddType(type);
						}
						codeDomBuilder.RunTransformations(transformAbortCondition);
						AddXmlDocumentation(options.DecompilerSettings, codeDomBuilder);
						codeDomBuilder.GenerateCode(new PlainTextOutput(w));
					}
				});
			AstMethodBodyBuilder.PrintNumberOfUnhandledOpcodes();
			return files.Select(f => Tuple.Create("Compile", f.Key)).Concat(WriteAssemblyInfo(dnSpyFileList, module, options, directories));
		}
		#endregion

		#region WriteResourceFilesInProject
		IEnumerable<Tuple<string, string>> WriteResourceFilesInProject(DnSpyFile assembly, DecompilationOptions options, HashSet<string> directories) {
			//AppDomain bamlDecompilerAppDomain = null;
			//try {
			foreach (EmbeddedResource r in assembly.ModuleDef.Resources.OfType<EmbeddedResource>()) {
				string fileName;
				Stream s = r.GetResourceStream();
				s.Position = 0;
				if (r.Name.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase)) {
					IEnumerable<DictionaryEntry> rs = null;
					try {
						rs = new ResourceSet(s).Cast<DictionaryEntry>();
					}
					catch {
						// NotSupportedException, IOException, BadImageFormatException, ArgumentException
						// and any other possible exception
					}
					if (rs != null && rs.All(e => e.Value is Stream)) {
						foreach (var pair in rs) {
							fileName = Path.Combine(((string)pair.Key).Split('/').Select(p => TextView.DecompilerTextView.CleanUpName(p)).ToArray());
							string dirName = Path.GetDirectoryName(fileName);
							if (!string.IsNullOrEmpty(dirName) && directories.Add(dirName)) {
								Directory.CreateDirectory(Path.Combine(options.SaveAsProjectDirectory, dirName));
							}
							Stream entryStream = (Stream)pair.Value;
							entryStream.Position = 0;
							if (fileName.EndsWith(".baml", StringComparison.OrdinalIgnoreCase)) {
								//									MemoryStream ms = new MemoryStream();
								//									entryStream.CopyTo(ms);
								// TODO implement extension point
								//									var decompiler = Baml.BamlResourceEntryNode.CreateBamlDecompilerInAppDomain(ref bamlDecompilerAppDomain, assembly.FileName);
								//									string xaml = null;
								//									try {
								//										xaml = decompiler.DecompileBaml(ms, assembly.FileName, new ConnectMethodDecompiler(assembly), new AssemblyResolver(assembly));
								//									}
								//									catch (XamlXmlWriterException) { } // ignore XAML writer exceptions
								//									if (xaml != null) {
								//										File.WriteAllText(Path.Combine(options.SaveAsProjectDirectory, Path.ChangeExtension(fileName, ".xaml")), xaml);
								//										yield return Tuple.Create("Page", Path.ChangeExtension(fileName, ".xaml"));
								//										continue;
								//									}
							}
							using (FileStream fs = new FileStream(Path.Combine(options.SaveAsProjectDirectory, fileName), FileMode.Create, FileAccess.Write)) {
								entryStream.CopyTo(fs);
							}
							yield return Tuple.Create("Resource", fileName);
						}
						continue;
					}
				}
				fileName = GetFileNameForResource(r.Name, directories);
				using (FileStream fs = new FileStream(Path.Combine(options.SaveAsProjectDirectory, fileName), FileMode.Create, FileAccess.Write)) {
					s.CopyTo(fs);
				}
				yield return Tuple.Create("EmbeddedResource", fileName);
			}
			//}
			//finally {
			//    if (bamlDecompilerAppDomain != null)
			//        AppDomain.Unload(bamlDecompilerAppDomain);
			//}
		}

		string GetFileNameForResource(string fullName, HashSet<string> directories) {
			string[] splitName = fullName.Split('.');
			string fileName = TextView.DecompilerTextView.CleanUpName(fullName);
			for (int i = splitName.Length - 1; i > 0; i--) {
				string ns = string.Join(".", splitName, 0, i);
				if (directories.Contains(ns)) {
					string name = string.Join(".", splitName, i, splitName.Length - i);
					fileName = Path.Combine(ns, TextView.DecompilerTextView.CleanUpName(name));
					break;
				}
			}
			return fileName;
		}
		#endregion

		AstBuilder CreateAstBuilder(DecompilationOptions options, ModuleDef currentModule = null, TypeDef currentType = null, bool isSingleMember = false) {
			if (currentModule == null)
				currentModule = currentType.Module;
			DecompilerSettings settings = options.DecompilerSettings;
			if (isSingleMember) {
				settings = settings.Clone();
				settings.UsingDeclarations = false;
			}
			return new AstBuilder(
				new DecompilerContext(currentModule) {
					CancellationToken = options.CancellationToken,
					CurrentType = currentType,
					Settings = settings
				}) {
				DontShowCreateMethodBodyExceptions = options.DontShowCreateMethodBodyExceptions,
			};
		}

		public override void TypeToString(ITextOutput output, ITypeDefOrRef type, bool includeNamespace, IHasCustomAttribute typeAttributes = null) {
			ConvertTypeOptions options = ConvertTypeOptions.IncludeTypeParameterDefinitions;
			if (includeNamespace)
				options |= ConvertTypeOptions.IncludeNamespace;

			TypeToString(output, options, type, typeAttributes);
		}

		bool WriteRefIfByRef(ITextOutput output, TypeSig typeSig, ParamDef pd) {
			if (typeSig.RemovePinnedAndModifiers() is ByRefSig) {
				if (pd != null && (!pd.IsIn && pd.IsOut)) {
					output.Write("out", TextTokenType.Keyword);
					output.WriteSpace();
				}
				else {
					output.Write("ref", TextTokenType.Keyword);
					output.WriteSpace();
				}
				return true;
			}
			return false;
		}

		void TypeToString(ITextOutput output, ConvertTypeOptions options, ITypeDefOrRef type, IHasCustomAttribute typeAttributes = null) {
			if (type == null)
				return;
			AstType astType = AstBuilder.ConvertType(type, typeAttributes, options);

			if (WriteRefIfByRef(output, type.TryGetByRefSig(), typeAttributes as ParamDef)) {
				if (astType is ComposedType && ((ComposedType)astType).PointerRank > 0)
					((ComposedType)astType).PointerRank--;
			}

			var module = type.Module;
			if (module == null && type is TypeSpec && ((TypeSpec)type).TypeSig.RemovePinnedAndModifiers() is GenericSig) {
				var sig = (GenericSig)((TypeSpec)type).TypeSig.RemovePinnedAndModifiers();
				if (sig.OwnerType != null)
					module = sig.OwnerType.Module;
				if (module == null && sig.OwnerMethod != null && sig.OwnerMethod.DeclaringType != null)
					module = sig.OwnerMethod.DeclaringType.Module;
			}
			var ctx = new DecompilerContext(type.Module);
			astType.AcceptVisitor(new CSharpOutputVisitor(new TextTokenWriter(output, ctx), FormattingOptionsFactory.CreateAllman()));
		}

		public override void FormatPropertyName(ITextOutput output, PropertyDef property, bool? isIndexer) {
			if (property == null)
				throw new ArgumentNullException("property");

			if (!isIndexer.HasValue) {
				isIndexer = property.IsIndexer();
			}
			if (isIndexer.Value) {
				var accessor = property.GetMethod ?? property.SetMethod;
				if (accessor != null && accessor.HasOverrides) {
					var methDecl = accessor.Overrides.First().MethodDeclaration;
					var declaringType = methDecl == null ? null : methDecl.DeclaringType;
					TypeToString(output, declaringType, includeNamespace: true);
					output.Write('.', TextTokenType.Operator);
				}
				output.Write("this", TextTokenType.Keyword);
				output.Write('[', TextTokenType.Operator);
				bool addSeparator = false;
				foreach (var p in property.PropertySig.GetParameters()) {
					if (addSeparator) {
						output.Write(',', TextTokenType.Operator);
						output.WriteSpace();
					}
					else
						addSeparator = true;
					TypeToString(output, p.ToTypeDefOrRef(), includeNamespace: true);
				}
				output.Write(']', TextTokenType.Operator);
			}
			else
				WriteIdentifier(output, property.Name, TextTokenHelper.GetTextTokenType(property));
		}

		static readonly HashSet<string> isKeyword = new HashSet<string>(StringComparer.Ordinal) {
			"abstract", "as", "base", "bool", "break", "byte", "case", "catch",
			"char", "checked", "class", "const", "continue", "decimal", "default", "delegate",
			"do", "double", "else", "enum", "event", "explicit", "extern", "false",
			"finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
			"in", "int", "interface", "internal", "is", "lock", "long", "namespace",
			"new", "null", "object", "operator", "out", "override", "params", "private",
			"protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
			"sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
			"true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
			"using", "virtual", "void", "volatile", "while",
		};

		static void WriteIdentifier(ITextOutput output, string id, TextTokenType tokenType) {
			if (isKeyword.Contains(id))
				output.Write('@', TextTokenType.Operator);
			output.Write(IdentifierEscaper.Escape(id), tokenType);
		}

		public override void FormatTypeName(ITextOutput output, TypeDef type) {
			if (type == null)
				throw new ArgumentNullException("type");

			TypeToString(output, ConvertTypeOptions.DoNotUsePrimitiveTypeNames | ConvertTypeOptions.IncludeTypeParameterDefinitions, type);
		}

		public override bool ShowMember(IMemberRef member) {
			return showAllMembers || !AstBuilder.MemberIsHidden(member, new DecompilationOptions().DecompilerSettings);
		}

		public override IMemberRef GetOriginalCodeLocation(IMemberRef member) {
			if (showAllMembers || !DecompilerSettingsPanel.CurrentDecompilerSettings.AnonymousMethods)
				return member;
			else
				return TreeNodes.Analyzer.Helpers.GetOriginalCodeLocation(member);
		}

		void WriteToolTipType(ITextOutput output, ITypeDefOrRef type, bool useNamespaces, bool usePrimitiveTypeName = true, IHasCustomAttribute typeAttributes = null) {
			var td = type as TypeDef;
			if (td == null && type is TypeRef)
				td = ((TypeRef)type).Resolve();
			if (td == null ||
				td.GenericParameters.Count == 0 ||
				(td.DeclaringType != null && td.DeclaringType.GenericParameters.Count >= td.GenericParameters.Count)) {
				var options = ConvertTypeOptions.IncludeTypeParameterDefinitions;
				if (useNamespaces)
					options |= ConvertTypeOptions.IncludeNamespace;
				if (!usePrimitiveTypeName)
					options |= ConvertTypeOptions.DoNotUsePrimitiveTypeNames;
				TypeToString(output, options, type, typeAttributes);
				return;
			}

			var typeSig = type.ToTypeSig();
			WriteRefIfByRef(output, typeSig, typeAttributes as ParamDef);

			int numGenParams = td.GenericParameters.Count;
			if (type.DeclaringType != null) {
				var options = ConvertTypeOptions.IncludeTypeParameterDefinitions;
				if (useNamespaces)
					options |= ConvertTypeOptions.IncludeNamespace;
				TypeToString(output, options, type.DeclaringType, null);
				output.Write('.', TextTokenType.Operator);
				numGenParams = numGenParams - td.DeclaringType.GenericParameters.Count;
				if (numGenParams < 0)
					numGenParams = 0;
			}
			else if (useNamespaces && !UTF8String.IsNullOrEmpty(td.Namespace)) {
				foreach (var ns in td.Namespace.String.Split('.')) {
					WriteIdentifier(output, ns, TextTokenType.NamespacePart);
					output.Write('.', TextTokenType.Operator);
				}
			}

			WriteIdentifier(output, RemoveGenericTick(td.Name), TextTokenHelper.GetTextTokenType(td));
			var genParams = td.GenericParameters.Skip(td.GenericParameters.Count - numGenParams).ToArray();
			WriteToolTipGenerics(output, genParams, TextTokenType.TypeGenericParameter);
		}

		void WriteToolTip(ITextOutput output, ITypeDefOrRef type, IHasCustomAttribute typeAttributes = null) {
			if (type == null)
				return;

			WriteToolTipType(output, type, TOOLTIP_USE_NAMESPACES, true, typeAttributes);
		}

		static GenericParam GetGenericParam(GenericSig gsig, GenericParamContext gpContext) {
			if (gsig == null)
				return null;
			ITypeOrMethodDef owner = gsig.IsTypeVar ? (ITypeOrMethodDef)gpContext.Type : gpContext.Method;
			if (owner == null)
				return null;
			foreach (var gp in owner.GenericParameters) {
				if (gp.Number == gsig.Number)
					return gp;
			}
			return null;
		}

		void WriteToolTip(ITextOutput output, TypeSig type, GenericParamContext gpContext, IHasCustomAttribute typeAttributes) {
			var gsig = type.RemovePinnedAndModifiers() as GenericSig;
			var gp = GetGenericParam(gsig, gpContext);
			if (gp != null) {
				WriteIdentifier(output, gp.Name, gsig.IsMethodVar ? TextTokenType.MethodGenericParameter : TextTokenType.TypeGenericParameter);
				return;
			}

			WriteToolTip(output, type.ToTypeDefOrRef(), typeAttributes);
		}

		void WriteToolTipGenerics(ITextOutput output, IList<GenericParam> gps, TextTokenType gpTokenType) {
			if (gps == null || gps.Count == 0)
				return;
			output.Write('<', TextTokenType.Operator);
			for (int i = 0; i < gps.Count; i++) {
				if (i > 0) {
					output.Write(',', TextTokenType.Operator);
					output.WriteSpace();
				}
				var gp = gps[i];
				if (gp.IsCovariant) {
					output.Write("out", TextTokenType.Keyword);
					output.WriteSpace();
				}
				else if (gp.IsContravariant) {
					output.Write("in", TextTokenType.Keyword);
					output.WriteSpace();
				}
				WriteIdentifier(output, gp.Name, gpTokenType);
			}
			output.Write('>', TextTokenType.Operator);
		}

		void WriteToolTipGenerics(ITextOutput output, IList<TypeSig> gps, TextTokenType gpTokenType, GenericParamContext gpContext) {
			if (gps == null || gps.Count == 0)
				return;
			output.Write('<', TextTokenType.Operator);
			for (int i = 0; i < gps.Count; i++) {
				if (i > 0) {
					output.Write(',', TextTokenType.Operator);
					output.WriteSpace();
				}
				WriteToolTip(output, gps[i], gpContext, null);
			}
			output.Write('>', TextTokenType.Operator);
		}

		void WriteToolTip(ITextOutput output, IMethod method) {
			WriteToolTipMethod(output, method);

			var td = method.DeclaringType.ResolveTypeDef();
			if (td != null) {
				int overloads = GetNumberOfOverloads(td, method.Name);
				if (overloads == 1)
					output.Write(string.Format(" (+ 1 overload)"), TextTokenType.Text);
				else if (overloads > 1)
					output.Write(string.Format(" (+ {0} overloads)", overloads), TextTokenType.Text);
			}
		}

		void WriteToolTipMethod(ITextOutput output, IMethod method) {
			var writer = new MethodWriter(this, output, method);
			writer.WriteReturnType();

			WriteToolTip(output, method.DeclaringType);
			output.Write('.', TextTokenType.Operator);
			if (writer.md != null && writer.md.IsConstructor && method.DeclaringType != null)
				WriteIdentifier(output, RemoveGenericTick(method.DeclaringType.Name), TextTokenHelper.GetTextTokenType(method));
			else if (writer.md != null && writer.md.Overrides.Count > 0) {
				var ovrMeth = (IMemberRef)writer.md.Overrides[0].MethodDeclaration;
				WriteToolTipType(output, ovrMeth.DeclaringType, false);
				output.Write('.', TextTokenType.Operator);
				WriteMethodName(output, method, ovrMeth.Name);
			}
			else
				WriteMethodName(output, method, method.Name);

			writer.WriteGenericArguments();
			writer.WriteMethodParameterList('(', ')');
		}

		void WriteMethodName(ITextOutput output, IMethod method, string name) {
			string[] list;
			if (nameToOperatorName.TryGetValue(name, out list)) {
				for (int i = 0; i < list.Length; i++) {
					if (i > 0)
						output.WriteSpace();
					var s = list[i];
					output.Write(s, 'a' <= s[0] && s[0] <= 'z' ? TextTokenType.Keyword : TextTokenType.Operator);
				}
			}
			else
				WriteIdentifier(output, name, TextTokenHelper.GetTextTokenType(method));
		}

		static int GetNumberOfOverloads(TypeDef type, string name) {
			//TODO: It counts every method, including methods the original type doesn't have access to.
			var hash = new HashSet<MethodDef>(MethodEqualityComparer.DontCompareDeclaringTypes);
			while (type != null) {
				foreach (var m in type.Methods) {
					if (m.Name == name)
						hash.Add(m);
				}
				type = type.BaseType.ResolveTypeDef();
			}
			return hash.Count - 1;
		}

		struct MethodWriter {
			readonly CSharpLanguage lang;
			readonly ITextOutput output;
			readonly IList<TypeSig> typeGenericParams;
			readonly IList<TypeSig> methodGenericParams;
			internal readonly MethodDef md;
			readonly MethodSig methodSig;

			public MethodWriter(CSharpLanguage lang, ITextOutput output, IMethod method) {
				this.lang = lang;
				this.output = output;
				this.typeGenericParams = null;
				this.methodGenericParams = null;
				this.methodSig = method.MethodSig;

				this.md = method as MethodDef;
				var ms = method as MethodSpec;
				var mr = method as MemberRef;
				if (ms != null) {
					var ts = ms.Method == null ? null : ms.Method.DeclaringType as TypeSpec;
					if (ts != null) {
						var gp = ts.TypeSig.RemovePinnedAndModifiers() as GenericInstSig;
						if (gp != null)
							typeGenericParams = gp.GenericArguments;
					}

					var gsSig = ms.GenericInstMethodSig;
					if (gsSig != null)
						methodGenericParams = gsSig.GenericArguments;

					this.md = ms.Method.ResolveMethodDef();
				}
				else if (mr != null) {
					var ts = mr.DeclaringType as TypeSpec;
					if (ts != null) {
						var gp = ts.TypeSig.RemovePinnedAndModifiers() as GenericInstSig;
						if (gp != null)
							typeGenericParams = gp.GenericArguments;
					}

					this.md = mr.ResolveMethod();
				}

				if (typeGenericParams != null || methodGenericParams != null)
					this.methodSig = GenericArgumentResolver.Resolve(methodSig, typeGenericParams, methodGenericParams);
			}

			public void WriteReturnType() {
				if (!(md != null && md.IsConstructor)) {
					lang.WriteToolTip(output, methodSig.RetType, GenericParamContext.Create(md), md == null ? null : md.Parameters.ReturnParameter.ParamDef);
					output.WriteSpace();
				}
			}

			public void WriteGenericArguments() {
				if (methodSig.GenParamCount > 0) {
					if (methodGenericParams != null)
						lang.WriteToolTipGenerics(output, methodGenericParams, TextTokenType.MethodGenericParameter, GenericParamContext.Create(md));
					else if (md != null)
						lang.WriteToolTipGenerics(output, md.GenericParameters, TextTokenType.MethodGenericParameter);
				}
			}

			public void WriteMethodParameterList(char lparen, char rparen) {
				output.Write(lparen, TextTokenType.Operator);
				int baseIndex = methodSig.HasThis ? 1 : 0;
				for (int i = 0; i < methodSig.Params.Count; i++) {
					if (i > 0) {
						output.Write(',', TextTokenType.Operator);
						output.WriteSpace();
					}
					ParamDef pd;
					if (md != null && baseIndex + i < md.Parameters.Count)
						pd = md.Parameters[baseIndex + i].ParamDef;
					else
						pd = null;
					if (pd != null && pd.CustomAttributes.IsDefined("System.ParamArrayAttribute")) {
						output.Write("params", TextTokenType.Keyword);
						output.WriteSpace();
					}
					var paramType = methodSig.Params[i];
					lang.WriteToolTip(output, paramType, GenericParamContext.Create(md), pd);
					output.WriteSpace();
					if (pd != null)
						WriteIdentifier(output, pd.Name, TextTokenType.Parameter);
				}
				output.Write(rparen, TextTokenType.Operator);
			}
		}

		static string RemoveGenericTick(string name) {
			int index = name.LastIndexOf('`');
			if (index < 0)
				return name;
			if (genericTick.IsMatch(name))
				return name.Substring(0, index);
			return name;
		}
		static readonly Regex genericTick = new Regex(@"`\d+$", RegexOptions.Compiled);

		void WriteToolTip(ITextOutput output, IField field) {
			var sig = field.FieldSig;
			var gpContext = GenericParamContext.Create(field.DeclaringType.ResolveTypeDef());
			bool isEnumOwner = gpContext.Type != null && gpContext.Type.IsEnum;

			var fd = field.ResolveFieldDef();
			if (!isEnumOwner) {
				if (fd != null && fd.IsLiteral)
					output.Write("(constant)", TextTokenType.Text);
				else
					output.Write("(field)", TextTokenType.Text);
				output.WriteSpace();
				WriteToolTip(output, sig.Type, gpContext, null);
				output.WriteSpace();
			}
			WriteToolTip(output, field.DeclaringType);
			output.Write('.', TextTokenType.Operator);
			WriteIdentifier(output, field.Name, TextTokenHelper.GetTextTokenType(field));
			if (fd.IsLiteral && fd.Constant != null) {
				output.WriteSpace();
				output.Write('=', TextTokenType.Operator);
				output.WriteSpace();
				WriteToolTipConstant(output, fd.Constant.Value);
			}
		}

		const bool USE_DECIMAL = false;
		void WriteToolTipConstant(ITextOutput output, object obj) {
			if (obj == null) {
				output.Write("null", TextTokenType.Keyword);
				return;
			}

			switch (Type.GetTypeCode(obj.GetType())) {
			case TypeCode.Boolean:
				output.Write((bool)obj ? "true" : "false", TextTokenType.Keyword);
				break;

			case TypeCode.Char:
				output.Write(NumberVMUtils.ToString((char)obj), TextTokenType.Char);
				break;

			case TypeCode.SByte:
				output.Write(NumberVMUtils.ToString((sbyte)obj, sbyte.MinValue, sbyte.MaxValue, USE_DECIMAL), TextTokenType.Number);
				break;

			case TypeCode.Byte:
				output.Write(NumberVMUtils.ToString((byte)obj, byte.MinValue, byte.MaxValue, USE_DECIMAL), TextTokenType.Number);
				break;

			case TypeCode.Int16:
				output.Write(NumberVMUtils.ToString((short)obj, short.MinValue, short.MaxValue, USE_DECIMAL), TextTokenType.Number);
				break;

			case TypeCode.UInt16:
				output.Write(NumberVMUtils.ToString((ushort)obj, ushort.MinValue, ushort.MaxValue, USE_DECIMAL), TextTokenType.Number);
				break;

			case TypeCode.Int32:
				output.Write(NumberVMUtils.ToString((int)obj, int.MinValue, int.MaxValue, USE_DECIMAL), TextTokenType.Number);
				break;

			case TypeCode.UInt32:
				output.Write(NumberVMUtils.ToString((uint)obj, uint.MinValue, uint.MaxValue, USE_DECIMAL), TextTokenType.Number);
				break;

			case TypeCode.Int64:
				output.Write(NumberVMUtils.ToString((long)obj, long.MinValue, long.MaxValue, USE_DECIMAL), TextTokenType.Number);
				break;

			case TypeCode.UInt64:
				output.Write(NumberVMUtils.ToString((ulong)obj, ulong.MinValue, ulong.MaxValue, USE_DECIMAL), TextTokenType.Number);
				break;

			case TypeCode.Single:
				output.Write(NumberVMUtils.ToString((float)obj), TextTokenType.Number);
				break;

			case TypeCode.Double:
				output.Write(NumberVMUtils.ToString((double)obj), TextTokenType.Number);
				break;

			case TypeCode.String:
				output.Write(NumberVMUtils.ToString((string)obj, true), TextTokenType.String);
				break;

			default:
				Debug.Fail(string.Format("Unknown constant: '{0}'", obj));
				output.Write(obj.ToString(), TextTokenType.Text);
				break;
			}
		}

		void WriteToolTip(ITextOutput output, PropertyDef prop) {
			var sig = prop.PropertySig;
			var md = prop.GetMethods.FirstOrDefault() ??
					prop.SetMethods.FirstOrDefault() ??
					prop.OtherMethods.FirstOrDefault();

			var writer = new MethodWriter(this, output, md);
			writer.WriteReturnType();
			WriteToolTip(output, prop.DeclaringType);
			output.Write('.', TextTokenType.Operator);
			var ovrMeth = md == null || md.Overrides.Count == 0 ? null : md.Overrides[0].MethodDeclaration;
			if (prop.IsIndexer()) {
				if (ovrMeth != null) {
					WriteToolTipType(output, ovrMeth.DeclaringType, false);
					output.Write('.', TextTokenType.Operator);
				}
				output.Write("this", TextTokenType.Keyword);
				writer.WriteGenericArguments();
				writer.WriteMethodParameterList('[', ']');
			}
			else if (ovrMeth != null && GetPropName(ovrMeth) != null) {
				WriteToolTipType(output, ovrMeth.DeclaringType, false);
				output.Write('.', TextTokenType.Operator);
				WriteIdentifier(output, GetPropName(ovrMeth), TextTokenHelper.GetTextTokenType(prop));
			}
			else
				WriteIdentifier(output, prop.Name, TextTokenHelper.GetTextTokenType(prop));

			output.WriteSpace();
			output.WriteLeftBrace();
			if (prop.GetMethods.Count > 0) {
				output.WriteSpace();
				output.Write("get", TextTokenType.Keyword);
				output.Write(';', TextTokenType.Operator);
			}
			if (prop.SetMethods.Count > 0) {
				output.WriteSpace();
				output.Write("set", TextTokenType.Keyword);
				output.Write(';', TextTokenType.Operator);
			}
			output.WriteSpace();
			output.WriteRightBrace();
		}

		static string GetPropName(IMethod method) {
			if (method == null)
				return null;
			var name = method.Name;
			if (name.StartsWith("get_", StringComparison.Ordinal) || name.StartsWith("set_", StringComparison.Ordinal))
				return name.Substring(4);
			return null;
		}

		void WriteToolTip(ITextOutput output, EventDef evt) {
			WriteToolTip(output, evt.EventType);
			output.WriteSpace();
			WriteToolTip(output, evt.DeclaringType);
			output.Write('.', TextTokenType.Operator);
			WriteIdentifier(output, evt.Name, TextTokenHelper.GetTextTokenType(evt));
		}

		void WriteToolTipWithClassInfo(ITextOutput output, ITypeDefOrRef type) {
			var td = type.ResolveTypeDef();

			MethodDef invoke;
			if (IsDelegate(td) && (invoke = td.FindMethod("Invoke")) != null && invoke.MethodSig != null) {
				output.Write("delegate", TextTokenType.Keyword);
				output.WriteSpace();

				var writer = new MethodWriter(this, output, invoke);
				writer.WriteReturnType();

				// Always print the namespace here because that's what VS does. I.e., ignore
				// TOOLTIP_USE_NAMESPACES.
				WriteToolTipType(output, td, true);

				writer.WriteGenericArguments();
				writer.WriteMethodParameterList('(', ')');
				return;
			}

			if (td == null) {
				base.WriteToolTip(output, type, null);
				return;
			}

			string keyword;
			if (td.IsEnum)
				keyword = "enum";
			else if (td.IsValueType)
				keyword = "struct";
			else if (td.IsInterface)
				keyword = "interface";
			else
				keyword = "class";
			output.Write(keyword, TextTokenType.Keyword);
			output.WriteSpace();

			// Always print the namespace here because that's what VS does. I.e., ignore
			// TOOLTIP_USE_NAMESPACES.
			WriteToolTipType(output, type, true, false);
		}

		static bool IsDelegate(TypeDef td) {
			return td != null &&
				new SigComparer().Equals(td.BaseType, td.Module.CorLibTypes.GetTypeRef("System", "MulticastDelegate")) &&
				td.BaseType.DefinitionAssembly.IsCorLib();
		}

		void WriteToolTip(ITextOutput output, GenericParam gp) {
			WriteIdentifier(output, gp.Name, TextTokenHelper.GetTextTokenType(gp));
			output.WriteSpace();
			output.Write("in", TextTokenType.Text);
			output.WriteSpace();

			var td = gp.Owner as TypeDef;
			if (td != null)
				WriteToolTipType(output, td, TOOLTIP_USE_NAMESPACES);
			else
				WriteToolTipMethod(output, gp.Owner as MethodDef);
		}

		// Don't show namespaces. It makes it easer to read the tooltip. Very rarely do you
		// really need the full name.
		const bool TOOLTIP_USE_NAMESPACES = false;

		public override void WriteToolTip(ITextOutput output, IMemberRef member, IHasCustomAttribute typeAttributes) {
			var method = member as IMethod;
			if (method != null && method.MethodSig != null) {
				WriteToolTip(output, method);
				return;
			}

			var field = member as IField;
			if (field != null && field.FieldSig != null) {
				WriteToolTip(output, field);
				return;
			}

			var prop = member as PropertyDef;
			if (prop != null && prop.PropertySig != null) {
				WriteToolTip(output, prop);
				return;
			}

			var evt = member as EventDef;
			if (evt != null && evt.EventType != null) {
				WriteToolTip(output, evt);
				return;
			}

			var tdr = member as ITypeDefOrRef;
			if (tdr != null) {
				WriteToolTipWithClassInfo(output, tdr);
				return;
			}

			var gp = member as GenericParam;
			if (gp != null) {
				WriteToolTip(output, gp);
				return;
			}

			base.WriteToolTip(output, member, typeAttributes);
		}

		public override void WriteToolTip(ITextOutput output, IVariable variable, string name) {
			var isLocal = variable is Local;
			output.Write(isLocal ? "(local variable)" : "(parameter)", TextTokenType.Text);
			output.WriteSpace();
			WriteToolTip(output, variable.Type, new GenericParamContext(), !isLocal ? ((Parameter)variable).ParamDef : null);
			output.WriteSpace();
			WriteIdentifier(output, GetName(variable, name), isLocal ? TextTokenType.Local : TextTokenType.Parameter);
		}
	}
}
