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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ICSharpCode.ILSpy.Controls {
	class DockedPane : Control {
		static DockedPane() {
			DefaultStyleKeyProperty.OverrideMetadata(typeof(DockedPane), new FrameworkPropertyMetadata(typeof(DockedPane)));
		}

		public DockedPane() {
			AddHandler(UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(_GotKeyboardFocus), true);
			AddHandler(UIElement.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(_LostKeyboardFocus), true);
			this.IsVisibleChanged += OnIsVisibleChanged;
		}

		void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
			IsActive = IsKeyboardFocusWithin;
		}

		void _GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
			IsActive = true;
		}

		void _LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
			IsActive = false;
		}

		protected override void OnMouseDown(MouseButtonEventArgs e) {
			base.OnMouseDown(e);
			if (!e.Handled) {
				var pane = Content;
				if (pane != null) {
					var uiElem = pane as UIElement;
					if (uiElem == null || !uiElem.IsKeyboardFocusWithin) {
						pane.FocusPane();
						e.Handled = true;
					}
				}
			}
		}

		public static readonly DependencyProperty IsActiveProperty =
			DependencyProperty.Register("IsActive", typeof(bool), typeof(DockedPane), new UIPropertyMetadata(true));

		public bool IsActive {
			get { return (bool)GetValue(IsActiveProperty); }
			set { SetValue(IsActiveProperty, value); }
		}

		public static readonly DependencyProperty TitleProperty =
			DependencyProperty.Register("Title", typeof(string), typeof(DockedPane));

		public string Title {
			get { return (string)GetValue(TitleProperty); }
			set { SetValue(TitleProperty, value); }
		}

		public static readonly DependencyProperty ContentProperty =
			DependencyProperty.Register("Content", typeof(IPane), typeof(DockedPane), new PropertyMetadata(null, OnContentPropertyChanged));

		static void OnContentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var dp = (DockedPane)d;
			var newUiElem = e.NewValue as UIElement;
			if (newUiElem != null)
				dp.IsActive = newUiElem.IsKeyboardFocusWithin;
		}

		public IPane Content {
			get { return (IPane)GetValue(ContentProperty); }
			set { SetValue(ContentProperty, value); }
		}

		public override void OnApplyTemplate() {
			base.OnApplyTemplate();
			var closeButton = this.Template.FindName("PART_Close", this) as ButtonBase;
			if (closeButton != null) {
				closeButton.Click += closeButton_Click;
			}
		}

		void closeButton_Click(object sender, RoutedEventArgs e) {
			if (CloseButtonClicked != null)
				CloseButtonClicked(this, e);
		}

		public event EventHandler CloseButtonClicked;

		protected override void OnKeyDown(KeyEventArgs e) {
			base.OnKeyDown(e);
			if (e.Key == Key.F4 && e.KeyboardDevice.Modifiers == ModifierKeys.Control || e.Key == Key.Escape) {
				if (CloseButtonClicked != null)
					CloseButtonClicked(this, e);
				e.Handled = true;
			}
		}
	}
}
