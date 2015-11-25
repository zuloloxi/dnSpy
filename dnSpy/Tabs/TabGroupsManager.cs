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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.ILSpy;

namespace dnSpy.Tabs {
	public class TabGroupEventArgs : EventArgs {
		public new static readonly TabGroupEventArgs Empty = new TabGroupEventArgs();
	}

	public class TabGroupSelectedEventArgs : TabGroupEventArgs {
		/// <summary>
		/// Old active index
		/// </summary>
		public int OldIndex { get; private set; }

		/// <summary>
		/// New active index
		/// </summary>
		public int NewIndex { get; private set; }

		public TabGroupSelectedEventArgs(int oldIndex, int newIndex) {
			this.OldIndex = oldIndex;
			this.NewIndex = newIndex;
		}
	}

	public class TabGroupSwappedEventArgs : TabGroupEventArgs {
		/// <summary>
		/// Index of first tab group
		/// </summary>
		public int Index1 { get; private set; }

		/// <summary>
		/// Index of second tab group
		/// </summary>
		public int Index2 { get; private set; }

		public TabGroupSwappedEventArgs(int index1, int index2) {
			this.Index1 = index1;
			this.Index2 = index2;
		}
	}

	sealed class TabGroupsManager<TState> where TState : TabState {
		bool isHorizontal;
		readonly ContentPresenter contentPresenter;
		readonly List<TabManager<TState>> tabManagers = new List<TabManager<TState>>();
		readonly Action<TabManager<TState>, TState, TState> onSelectionChanged;
		readonly Action<TabManager<TState>, TabManagerAddType, TState> onAddRemoveTabState;

		public int ActiveIndex {
			get { return _activeIndex; }
			private set { _activeIndex = value; }
		}
		int _activeIndex;

		public TabManager<TState> ActiveTabGroup {
			get { return tabManagers[ActiveIndex]; }
		}

		public IList<TabManager<TState>> AllTabGroups {
			get { return tabManagers; }
		}

		public int NumberOfTabGroups {
			get { return tabManagers.Count; }
		}

		public bool IsHorizontal {
			get { return isHorizontal; }
		}

		public bool IsVertical {
			get { return !isHorizontal; }
		}

		public IEnumerable<TState> AllTabStates {
			get {
				foreach (var tabManager in AllTabGroups) {
					foreach (var tabState in tabManager.AllTabStates)
						yield return tabState;
				}
			}
		}

		public TabGroupsManager(ContentPresenter contentPresenter, Action<TabManager<TState>, TState, TState> onSelectionChanged, Action<TabManager<TState>, TabManagerAddType, TState> onAddRemoveTabState) {
			dntheme.Themes.ThemeChanged += Themes_ThemeChanged;
			this.contentPresenter = contentPresenter;
			this.onSelectionChanged = onSelectionChanged;
			this.onAddRemoveTabState = onAddRemoveTabState;
			CreateTabManager(0);
			UpdateGrid();
		}

		void Themes_ThemeChanged(object sender, EventArgs e) {
			foreach (var tabManager in tabManagers)
				tabManager.OnThemeChanged();
		}

		public event EventHandler<TabGroupEventArgs> OnTabGroupAdded;
		public event EventHandler<TabGroupEventArgs> OnTabGroupRemoved;
		public event EventHandler<TabGroupSelectedEventArgs> OnTabGroupSelected;
		public event EventHandler<TabGroupSwappedEventArgs> OnTabGroupSwapped;
		public event EventHandler<TabGroupEventArgs> OnOrientationChanged;

		void UpdateOrientation(bool horizontal) {
			if (this.isHorizontal == horizontal)
				return;
			this.isHorizontal = horizontal;
			if (OnOrientationChanged != null)
				OnOrientationChanged(this, TabGroupEventArgs.Empty);
		}

		internal TabManager<TState> CreateTabGroup(bool horizontal) {
			Debug.Assert(tabManagers.Count == 1 || this.isHorizontal == horizontal);
			var tabManager = CreateTabManager(tabManagers.Count);
			UpdateGrid(horizontal);
			UpdateOrientation(horizontal);
			return tabManager;
		}

		TabManager<TState> CreateTabManager(int insertIndex) {
			var tabControl = new TabControl();
			tabControl.Style = App.Current.FindResource("TabStateTabControl") as Style;
			var tabManager = new TabManager<TState>(this, tabControl, onSelectionChanged, onAddRemoveTabState);
			tabManagers.Insert(insertIndex, tabManager);
			ContextMenuProvider.Add(tabManager.TabControl);
			if (OnTabGroupAdded != null)
				OnTabGroupAdded(this, TabGroupEventArgs.Empty);
			return tabManager;
		}

		public bool IsTabGroup(TabControl tabControl) {
			if (tabControl == null)
				return false;
			foreach (var mgr in tabManagers) {
				if (mgr.TabControl == tabControl)
					return true;
			}
			return false;
		}

		public void SetSelectedIndex(int index) {
			if (index < 0 || index >= tabManagers.Count)
				index = 0;
			SetActive(tabManagers[index]);
		}

		public void SetActive(TabManager<TState> tabManager) {
			if (tabManager == tabManagers[ActiveIndex])
				return;
			int newIndex = tabManagers.IndexOf(tabManager);
			if (newIndex < 0)
				throw new InvalidOperationException();
			int oldIndex = ActiveIndex;
			ActiveIndex = newIndex;
			if (OnTabGroupSelected != null)
				OnTabGroupSelected(this, new TabGroupSelectedEventArgs(oldIndex, newIndex));
		}

		public void Remove(TabManager<TState> tabManager) {
			int index = tabManagers.IndexOf(tabManager);
			Debug.Assert(index >= 0);
			if (index < 0)
				return;
			if (tabManagers.Count <= 1)
				return;
			if (ActiveIndex == index) {
				int newIndex = index == 0 ? index + 1 : index - 1;
				SetActive(tabManagers[newIndex]);
				ActiveIndex = newIndex;
			}
			var current = tabManagers[ActiveIndex];
			tabManagers.Remove(tabManager);
			ActiveIndex = tabManagers.IndexOf(current);
			Debug.Assert(ActiveIndex >= 0);
			if (OnTabGroupRemoved != null)
				OnTabGroupRemoved(this, TabGroupEventArgs.Empty);
			UpdateGrid();
		}

		public bool NewHorizontalTabGroupCanExecute() {
			return (tabManagers.Count == 1 || isHorizontal) &&
				tabManagers[ActiveIndex].Count > 1;
		}

		public void NewHorizontalTabGroup() {
			if (!NewHorizontalTabGroupCanExecute())
				return;
			AddNewTabGroup(true);
		}

		public bool NewVerticalTabGroupCanExecute() {
			return (tabManagers.Count == 1 || !isHorizontal) &&
				tabManagers[ActiveIndex].Count > 1;
		}

		public void NewVerticalTabGroup() {
			if (!NewVerticalTabGroupCanExecute())
				return;
			AddNewTabGroup(false);
		}

		void AddNewTabGroup(bool horizontal) {
			Debug.Assert(tabManagers.Count == 1 || isHorizontal == horizontal);

			var newTabManager = CreateTabManager(ActiveIndex + 1);

			UpdateGrid(horizontal);
			UpdateOrientation(horizontal);

			Move(newTabManager, tabManagers[ActiveIndex], tabManagers[ActiveIndex].ActiveTabState);
			SetActive(newTabManager);
		}

		void Move(TabManager<TState> dstTabManager, TabManager<TState> srcTabManager, TState srcTabState, int insertIndex = 0) {
			Debug.Assert(tabManagers.Contains(dstTabManager));
			Debug.Assert(tabManagers.Contains(srcTabManager));
			Debug.Assert(srcTabManager.TabControl.Items.Contains(srcTabState.TabItem));
			if (srcTabManager.MoveToAndSelect(dstTabManager, srcTabState, insertIndex))
				SetActive(dstTabManager);
		}

		internal bool MoveToNextTabGroupCanExecute() {
			return ActiveIndex + 1 < tabManagers.Count &&
				tabManagers[ActiveIndex].ActiveTabState != null;
		}

		internal void MoveToNextTabGroup() {
			if (!MoveToNextTabGroupCanExecute())
				return;
			Move(tabManagers[ActiveIndex + 1], tabManagers[ActiveIndex], tabManagers[ActiveIndex].ActiveTabState);
		}

		internal bool MoveToPreviousTabGroupCanExecute() {
			return ActiveIndex != 0 &&
				tabManagers[ActiveIndex].ActiveTabState != null;
		}

		internal void MoveToPreviousTabGroup() {
			if (!MoveToPreviousTabGroupCanExecute())
				return;
			Move(tabManagers[ActiveIndex - 1], tabManagers[ActiveIndex], tabManagers[ActiveIndex].ActiveTabState);
		}

		internal bool MoveAllToNextTabGroupCanExecute() {
			return ActiveIndex + 1 < tabManagers.Count &&
				tabManagers[ActiveIndex].Count > 1;
		}

		internal void MoveAllToNextTabGroup() {
			if (!MoveAllToNextTabGroupCanExecute())
				return;
			MoveAllToOtherTabGroup(tabManagers[ActiveIndex + 1], tabManagers[ActiveIndex]);
		}

		internal bool MoveAllToPreviousTabGroupCanExecute() {
			return ActiveIndex != 0 &&
				tabManagers[ActiveIndex].Count > 1;
		}

		internal void MoveAllToPreviousTabGroup() {
			if (!MoveToPreviousTabGroupCanExecute())
				return;
			MoveAllToOtherTabGroup(tabManagers[ActiveIndex - 1], tabManagers[ActiveIndex]);
		}

		void MoveAllToOtherTabGroup(TabManager<TState> dst, TabManager<TState> src) {
			var activeTab = src.ActiveTabState;
			Merge(dst, src, 0);
			dst.SetSelectedTab(activeTab);
			SetActive(dst);
		}

		internal bool CloseAllTabsCanExecute() {
			foreach (var tabManager in AllTabGroups) {
				if (tabManager.CloseAllTabsCanExecute())
					return true;
			}
			return false;
		}

		internal void CloseAllTabs() {
			if (!CloseAllTabsCanExecute())
				return;
			foreach (var tabManager in AllTabGroups.ToArray())
				tabManager.CloseAllTabs();
		}

		void Merge(TabManager<TState> dstTabManager, TabManager<TState> srcTabManager, int insertIndex) {
			if (dstTabManager == srcTabManager)
				return;
			if (insertIndex < 0 || insertIndex > dstTabManager.Count)
				insertIndex = dstTabManager.Count;
			foreach (var srcTabState in srcTabManager.AllTabStates.ToArray())
				srcTabManager.MoveTo(dstTabManager, srcTabState, insertIndex++);
		}

		internal bool MergeAllTabGroupsCanExecute() {
			return tabManagers.Count > 1;
		}

		internal void MergeAllTabGroups() {
			if (!MergeAllTabGroupsCanExecute())
				return;
			var dstTabManager = tabManagers[ActiveIndex];
			foreach (var tabManager in tabManagers.ToArray()) {
				if (tabManager == dstTabManager)
					continue;
				Merge(dstTabManager, tabManager, -1);
			}
		}

		internal bool UseVerticalTabGroupsCanExecute() {
			return tabManagers.Count > 1 && isHorizontal;
		}

		internal void UseVerticalTabGroups() {
			if (!UseVerticalTabGroupsCanExecute())
				return;
			SwitchGridOrientation(false);
		}

		internal bool UseHorizontalTabGroupsCanExecute() {
			return tabManagers.Count > 1 && !isHorizontal;
		}

		internal void UseHorizontalTabGroups() {
			if (!UseHorizontalTabGroupsCanExecute())
				return;
			SwitchGridOrientation(true);
		}

		internal bool CloseTabGroupCanExecute() {
			return tabManagers.Count > 1;
		}

		internal void CloseTabGroup() {
			if (!CloseTabGroupCanExecute())
				return;
			tabManagers[ActiveIndex].CloseAllTabs();
		}

		internal bool CloseAllTabGroupsButThisCanExecute() {
			return tabManagers.Count > 1;
		}

		internal void CloseAllTabGroupsButThis() {
			if (!CloseAllTabGroupsButThisCanExecute())
				return;
			var activeTabManager = tabManagers[ActiveIndex];
			foreach (var tabManager in tabManagers.ToArray()) {
				if (activeTabManager == tabManager)
					continue;
				tabManager.CloseAllTabs();
			}
		}

		internal bool MoveTabGroupAfterNextTabGroupCanExecute() {
			return ActiveIndex + 1 < tabManagers.Count;
		}

		internal void MoveTabGroupAfterNextTabGroup() {
			if (!MoveTabGroupAfterNextTabGroupCanExecute())
				return;
			SwapTabGroups(ActiveIndex, ActiveIndex + 1);
		}

		internal bool MoveTabGroupBeforePreviousTabGroupCanExecute() {
			return ActiveIndex != 0;
		}

		internal void MoveTabGroupBeforePreviousTabGroup() {
			if (!MoveTabGroupBeforePreviousTabGroupCanExecute())
				return;
			SwapTabGroups(ActiveIndex - 1, ActiveIndex);
		}

		void SwapTabGroups(int index1, int index2) {
			var tmp1 = tabManagers[index1];
			tabManagers[index1] = tabManagers[index2];
			tabManagers[index2] = tmp1;

			if (ActiveIndex == index1)
				ActiveIndex = index2;
			else if (ActiveIndex == index2)
				ActiveIndex = index1;

			UpdateGrid();

			if (OnTabGroupSwapped != null)
				OnTabGroupSwapped(this, new TabGroupSwappedEventArgs(index1, index2));
		}

		void UpdateGrid() {
			UpdateGrid(isHorizontal);
		}

		void UpdateGrid(bool horizontal) {
			var grid = new Grid();
			grid.Style = App.Current.FindResource("TabGroupsGridStyle") as Style;
			var oldGrid = contentPresenter.Content as Grid;
			if (oldGrid != null)
				oldGrid.Children.Clear();
			contentPresenter.Content = null;

			// Make sure the horizontal grid splitters can resize the content
			double d = 0.0001;
			if (horizontal) {
				grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
				int rowCol = 0;
				foreach (var tabManager in tabManagers) {
					if (grid.Children.Count > 0) {
						var gridSplitter = new GridSplitter();
						Panel.SetZIndex(gridSplitter, 1);
						grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(3, GridUnitType.Pixel) });
						gridSplitter.SetValue(Grid.RowProperty, rowCol);
						gridSplitter.Margin = new Thickness(0, -5, 0, -5);
						gridSplitter.BorderThickness = new Thickness(0, 5, 0, 5);
						gridSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
						gridSplitter.VerticalAlignment = VerticalAlignment.Center;
						gridSplitter.Focusable = false;
						gridSplitter.BorderBrush = Brushes.Transparent;
						grid.Children.Add(gridSplitter);
						rowCol++;
					}

					grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1 - d, GridUnitType.Star) });
					tabManager.TabControl.SetValue(Grid.RowProperty, rowCol);
					tabManager.TabControl.ClearValue(Grid.ColumnProperty);
					grid.Children.Add(tabManager.TabControl);
					rowCol++;
					d = -d;
				}
			}
			else {
				grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
				int rowCol = 0;
				foreach (var tabManager in tabManagers) {
					if (grid.Children.Count > 0) {
						var gridSplitter = new GridSplitter();
						Panel.SetZIndex(gridSplitter, 1);
						grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(3, GridUnitType.Pixel) });
						gridSplitter.SetValue(Grid.ColumnProperty, rowCol);
						gridSplitter.Margin = new Thickness(-5, 0, -5, 0);
						gridSplitter.BorderThickness = new Thickness(5, 0, 5, 0);
						gridSplitter.HorizontalAlignment = HorizontalAlignment.Center;
						gridSplitter.VerticalAlignment = VerticalAlignment.Stretch;
						gridSplitter.Focusable = false;
						gridSplitter.BorderBrush = Brushes.Transparent;
						grid.Children.Add(gridSplitter);
						rowCol++;
					}

					grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1 - d, GridUnitType.Star) });
					tabManager.TabControl.ClearValue(Grid.RowProperty);
					tabManager.TabControl.SetValue(Grid.ColumnProperty, rowCol);
					grid.Children.Add(tabManager.TabControl);
					rowCol++;
					d = -d;
				}
			}
			contentPresenter.Content = grid;
		}

		void SwitchGridOrientation(bool horizontal) {
			if (isHorizontal == horizontal)
				return;
			UpdateGrid(horizontal);
			UpdateOrientation(horizontal);
		}

		public bool SetActiveTab(TState tabState) {
			var tabManager = tabState.Owner as TabManager<TState>;
			if (tabManager == null || !tabManagers.Contains(tabManager))
				return false;
			if (!tabManager.SetActiveTab(tabState))
				return false;
			SetActive(tabManager);
			return true;
		}
	}
}
