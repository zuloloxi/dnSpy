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
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.TreeNodes;

namespace dnSpy.Images {
	public enum BackgroundType {
		Button,
		TextEditor,
		DialogWindow,
		TextBox,
		TreeNode,
		Search,
		ComboBox,
		Toolbar,
		ToolBarButtonChecked,
		MainMenuMenuItem,
		ContextMenuItem,
		GridViewItem,
		CodeToolTip,
		TitleAreaActive,
		TitleAreaInactive,
		CommandBar,
	}

	public struct ImageInfo {
		public readonly string Name;
		public readonly BackgroundType BackgroundType;

		public ImageInfo(string name, BackgroundType bgType) {
			this.Name = name;
			this.BackgroundType = bgType;
		}
	}

	public sealed class ImageCache {
		public static readonly ImageCache Instance = new ImageCache();

		readonly object lockObj = new object();
		readonly Dictionary<Tuple<string, Color>, BitmapSource> imageCache = new Dictionary<Tuple<string, Color>, BitmapSource>();
		bool isHighContrast;

		internal void OnThemeChanged() {
			lock (lockObj) {
				imageCache.Clear();
				isHighContrast = dntheme.Themes.Theme.IsHighContrast;
			}
		}

		public BitmapSource GetImage(ImageInfo info) {
			if (info.Name == null)
				return null;
			return GetImage(info.Name, info.BackgroundType);
		}

		public BitmapSource GetImage(string name, BackgroundType bgType) {
			return GetImage(name, GetColor(bgType));
		}

		public static Color GetColor(BackgroundType bgType) {
			switch (bgType) {
			case BackgroundType.Button: return GetColorBackground(dntheme.ColorType.CommonControlsButtonIconBackground);
			case BackgroundType.TextEditor: return GetColorBackground(dntheme.ColorType.DefaultText);
			case BackgroundType.DialogWindow: return GetColorBackground(dntheme.ColorType.DialogWindow);
			case BackgroundType.TextBox: return GetColorBackground(dntheme.ColorType.CommonControlsTextBox);
			case BackgroundType.TreeNode: return GetColorBackground(dntheme.ColorType.TreeView);
			case BackgroundType.Search: return GetColorBackground(dntheme.ColorType.ListBoxBackground);
			case BackgroundType.ComboBox: return GetColorBackground(dntheme.ColorType.CommonControlsComboBoxBackground);
			case BackgroundType.Toolbar: return GetColorBackground(dntheme.ColorType.ToolBarIconBackground);
			case BackgroundType.ToolBarButtonChecked: return GetColorBackground(dntheme.ColorType.ToolBarButtonChecked);
			case BackgroundType.MainMenuMenuItem: return GetColorBackground(dntheme.ColorType.ToolBarIconVerticalBackground);
			case BackgroundType.ContextMenuItem: return GetColorBackground(dntheme.ColorType.ContextMenuRectangleFill);
			case BackgroundType.GridViewItem: return GetColorBackground(dntheme.ColorType.GridViewBackground);
			case BackgroundType.CodeToolTip: return GetColorBackground(dntheme.ColorType.CodeToolTip);
			case BackgroundType.TitleAreaActive: return GetColorBackground(dntheme.ColorType.EnvironmentMainWindowActiveCaption);
			case BackgroundType.TitleAreaInactive: return GetColorBackground(dntheme.ColorType.EnvironmentMainWindowInactiveCaption);
			case BackgroundType.CommandBar: return GetColorBackground(dntheme.ColorType.EnvironmentCommandBarIcon);
			default:
				Debug.Fail("Invalid bg type");
				return GetColorBackground(dntheme.ColorType.SystemColorsWindow);
			}
		}

		static Color GetColorBackground(dntheme.ColorType colorType) {
			var c = dntheme.Themes.Theme.GetColor(colorType).InheritedColor.Background.GetColor(null);
			Debug.WriteLineIf(c == null, string.Format("Background color is null: {0}", colorType));
			return c.Value;
		}

		static string GetUri(object part, string icon) {
			var assembly = part.GetType().Assembly;
			var name = assembly.GetName();
			return "pack://application:,,,/" + name.Name + ";v" + name.Version + ";component/" + icon;
		}

		public BitmapSource GetImage(string name, Color bgColor) {
			return GetImage(this, name, bgColor);
		}

		public BitmapSource GetImage(object part, string icon, BackgroundType bgType) {
			return GetImage(part.GetType().Assembly, icon, GetColor(bgType));
		}

		public BitmapSource GetImage(Assembly asm, string icon, BackgroundType bgType) {
			return GetImage(asm, icon, GetColor(bgType));
		}

		public BitmapSource GetImage(object part, string icon, Color bgColor) {
			return GetImage(part.GetType().Assembly, icon, bgColor);
		}

		public BitmapSource GetImage(Assembly asm, string icon, Color bgColor) {
			var name = asm.GetName();
			var uri = "pack://application:,,,/" + name.Name + ";v" + name.Version + ";component/Images/" + icon + ".png";
			return GetImageUsingUri(uri, bgColor);
		}

		public BitmapSource GetImageUsingUri(string key, BackgroundType bgType) {
			return GetImageUsingUri(key, GetColor(bgType));
		}

		public BitmapSource GetImageUsingUri(string uri, Color bgColor) {
			var key = Tuple.Create(uri, bgColor);
			BitmapSource image;
			lock (lockObj) {
				if (imageCache.TryGetValue(key, out image))
					return image;

				image = ThemedImageCreator.CreateThemedBitmapSource(new BitmapImage(new Uri(uri)), bgColor, isHighContrast);
				imageCache.Add(key, image);
			}
			return image;
		}

		public static ImageSource GetIcon(TypeIcon icon, BackgroundType bgType) {
			return TypeTreeNode.GetIcon(icon, bgType);
		}

		public static ImageSource GetIcon(MemberIcon icon, BackgroundType bgType) {
			return FieldTreeNode.GetIcon(icon, bgType);
		}
	}
}
