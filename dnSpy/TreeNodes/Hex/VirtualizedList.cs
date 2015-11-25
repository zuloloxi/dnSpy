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
using System.Collections;
using System.Diagnostics;

namespace dnSpy.TreeNodes.Hex {
	interface IVirtualizedListItem {
		int Index { get; }
	}

	// The key to make this work is to implement IList, yes, IList, not IList<T>.
	sealed class VirtualizedList<T> : IList where T : class, IVirtualizedListItem {
		readonly WeakReference[] list;
		readonly Func<int, T> createItem;

		public VirtualizedList(int count, Func<int, T> createItem) {
			this.list = new WeakReference[count];
			this.createItem = createItem;
		}

		public T TryGet(int index) {
			Debug.Assert(0 <= index && index < list.Length);
			if ((uint)index >= (uint)list.Length)
				return null;
			var weakRef = list[index];
			return weakRef == null ? null : (T)weakRef.Target;
		}

		public T this[int index] {
			get {
				T obj;
				var weakRef = list[index];
				if (weakRef == null) {
					list[index] = new WeakReference(obj = createItem(index));
					return obj;
				}

				obj = (T)weakRef.Target;
				if (obj == null)
					weakRef.Target = obj = createItem(index);
				return obj;
			}
		}

		object IList.this[int index] {
			get { return this[index]; }
			set { Debug.Fail("Method shouldn't be called"); }
		}

		public int Count {
			get { return list.Length; }
		}

		bool IList.IsFixedSize {
			get { return true; }
		}

		bool IList.IsReadOnly {
			get { return true; }
		}

		bool ICollection.IsSynchronized {
			get { return false; }
		}

		object ICollection.SyncRoot {
			get { return this; }
		}

		int IList.Add(object value) {
			Debug.Fail("Method shouldn't be called");
			return -1;
		}

		void IList.Clear() {
			Debug.Fail("Method shouldn't be called");
		}

		bool IList.Contains(object value) {
			return value is IVirtualizedListItem;
		}

		void ICollection.CopyTo(Array array, int index) {
			Debug.Fail("Method shouldn't be called");
			throw new NotImplementedException("ICollection.CopyTo shouldn't be called");
		}

		IEnumerator IEnumerable.GetEnumerator() {
			for (int i = 0; i < list.Length; i++)
				yield return this[i];
		}

		int IList.IndexOf(object value) {
			var item = value as IVirtualizedListItem;
			return item == null ? -1 : item.Index;
		}

		void IList.Insert(int index, object value) {
			Debug.Fail("Method shouldn't be called");
		}

		void IList.Remove(object value) {
			Debug.Fail("Method shouldn't be called");
		}

		void IList.RemoveAt(int index) {
			Debug.Fail("Method shouldn't be called");
		}
	}
}
