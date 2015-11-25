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
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using dnlib.DotNet;
using dnlib.DotNet.Resources;
using dnSpy.AsmEditor.Resources;
using ICSharpCode.Decompiler;
using ICSharpCode.ILSpy;

namespace dnSpy.TreeNodes {
	[Export(typeof(IResourceFactory<ResourceElement, ResourceElementTreeNode>))]
	sealed class SerializedImageResourceElementTreeNodeFactory : IResourceFactory<ResourceElement, ResourceElementTreeNode> {
		public int Priority {
			get { return 100; }
		}

		public ResourceElementTreeNode Create(ModuleDef module, ResourceElement resInput) {
			var serializedData = resInput.ResourceData as BinaryResourceData;
			if (serializedData == null)
				return null;

			byte[] imageData;
			if (GetImageData(module, serializedData.TypeName, serializedData.Data, out imageData))
				return new SerializedImageResourceElementTreeNode(resInput, imageData);

			return null;
		}

		internal static bool GetImageData(ModuleDef module, string typeName, byte[] serializedData, out byte[] imageData) {
			imageData = null;
			if (CouldBeBitmap(module, typeName)) {
				var dict = Deserializer.Deserialize(SystemDrawingBitmap.DefinitionAssembly.FullName, SystemDrawingBitmap.ReflectionFullName, serializedData);
				// Bitmap loops over every item looking for "Data" (case insensitive)
				foreach (var v in dict.Values) {
					var d = v.Value as byte[];
					if (d == null)
						continue;
					if ("Data".Equals(v.Name, StringComparison.OrdinalIgnoreCase)) {
						imageData = d;
						return true;
					}
				}
				return false;
			}

			if (CouldBeIcon(module, typeName)) {
				var dict = Deserializer.Deserialize(SystemDrawingIcon.DefinitionAssembly.FullName, SystemDrawingIcon.ReflectionFullName, serializedData);
				DeserializedDataInfo info;
				if (!dict.TryGetValue("IconData", out info))
					return false;
				imageData = info.Value as byte[];
				return imageData != null;
			}

			return false;
		}

		static bool CouldBeBitmap(ModuleDef module, string name) {
			return CheckType(module, name, SystemDrawingBitmap);
		}

		static bool CouldBeIcon(ModuleDef module, string name) {
			return CheckType(module, name, SystemDrawingIcon);
		}

		internal static bool CheckType(ModuleDef module, string name, TypeRef expectedType) {
			if (module == null)
				module = new ModuleDefUser();
			var tr = TypeNameParser.ParseReflection(module, name, null);
			if (tr == null)
				return false;

			var flags = AssemblyNameComparerFlags.All & ~AssemblyNameComparerFlags.Version;
			if (!new AssemblyNameComparer(flags).Equals(tr.DefinitionAssembly, expectedType.DefinitionAssembly))
				return false;

			if (!new SigComparer().Equals(tr, expectedType))
				return false;

			return true;
		}
		static readonly AssemblyRef SystemDrawingAsm = new AssemblyRefUser(new AssemblyNameInfo("System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"));
		static readonly TypeRef SystemDrawingBitmap = new TypeRefUser(null, "System.Drawing", "Bitmap", SystemDrawingAsm);
		static readonly TypeRef SystemDrawingIcon = new TypeRefUser(null, "System.Drawing", "Icon", SystemDrawingAsm);
	}

	sealed class SerializedImageResourceElementTreeNode : ResourceElementTreeNode {
		public ImageSource ImageSource {
			get { return imageSource; }
		}
		ImageSource imageSource;
		byte[] imageData;

		public override string IconName {
			get { return "ImageFile"; }
		}

		public SerializedImageResourceElementTreeNode(ResourceElement resElem, byte[] imageData)
			: base(resElem) {
			InitializeImageData(imageData);
		}

		void InitializeImageData(byte[] imageData) {
			this.imageData = imageData;
			this.imageSource = ImageResourceElementTreeNode.CreateImageSource(imageData);
		}

		public override void Decompile(Language language, ITextOutput output) {
			var smartOutput = output as ISmartTextOutput;
			if (smartOutput != null) {
				smartOutput.AddUIElement(() => {
					return new Image {
						Source = ImageSource,
					};
				});
			}

			base.Decompile(language, output);
		}

		protected override IEnumerable<ResourceData> GetDeserialized() {
			yield return new ResourceData(resElem.Name, () => new MemoryStream(imageData));
		}

		internal ResourceElement GetAsRawImage() {
			return new ResourceElement {
				Name = resElem.Name,
				ResourceData = new BuiltInResourceData(ResourceTypeCode.ByteArray, imageData),
			};
		}

		internal ResourceElement Serialize(ResourceElement resElem) {
			var data = (byte[])((BuiltInResourceData)resElem.ResourceData).Data;
			bool isIcon = BitConverter.ToUInt32(data, 0) == 0x00010000;

			object obj;
			if (isIcon)
				obj = new System.Drawing.Icon(new MemoryStream(data));
			else
				obj = new System.Drawing.Bitmap(new MemoryStream(data));

			return new ResourceElement {
				Name = resElem.Name,
				ResourceData = new BinaryResourceData(new UserResourceType(obj.GetType().AssemblyQualifiedName, ResourceTypeCode.UserTypes), SerializationUtils.Serialize(obj)),
			};
		}

		public override string CheckCanUpdateData(ResourceElement newResElem) {
			var res = base.CheckCanUpdateData(newResElem);
			if (!string.IsNullOrEmpty(res))
				return res;

			var binData = (BinaryResourceData)newResElem.ResourceData;
			byte[] imageData;
			if (!SerializedImageResourceElementTreeNodeFactory.GetImageData(GetModule(this), binData.TypeName, binData.Data, out imageData))
				return "The new data is not an image.";

			try {
				ImageResourceElementTreeNode.CreateImageSource(imageData);
			}
			catch {
				return "The new data is not an image.";
			}

			return string.Empty;
		}

		public override void UpdateData(ResourceElement newResElem) {
			base.UpdateData(newResElem);

			var binData = (BinaryResourceData)newResElem.ResourceData;
			byte[] imageData;
			SerializedImageResourceElementTreeNodeFactory.GetImageData(GetModule(this), binData.TypeName, binData.Data, out imageData);
			InitializeImageData(imageData);
		}

		public override NodePathName NodePathName {
			get { return new NodePathName("serimgresel", UIUtils.CleanUpName(resElem.Name)); }
		}
	}
}
