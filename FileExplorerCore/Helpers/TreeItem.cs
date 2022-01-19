using System;
using System.Collections.Generic;
using System.Linq;

namespace FileExplorerCore.Helpers
{
	public class TreeItem<T>
	{
		public List<TreeItem<T>> Children { get; }
		public TreeItem<T>? Parent { get; private set; }

		public T Value { get; set; }

		public bool HasParent => Parent is not null;
		public bool HasChildren => Children.Count > 0;

		public TreeItem<T> this[int index]
		{
			get => Children[index];
			set => Children[index] = value;
		}

		public TreeItem(T value, IEnumerable<TreeItem<T>>? children = null, TreeItem<T>? parent = null)
		{
			Children = new List<TreeItem<T>>(children ?? Enumerable.Empty<TreeItem<T>>());

			Parent = parent;
			Value = value;
		}

		/// <summary>
		/// Get the root of the tree
		/// </summary>
		/// <returns>the root of the tree</returns>
		public TreeItem<T>? GetRoot()
		{
			var parent = this;

			while (parent is { HasParent: true })
			{
				parent = parent.Parent;
			}

			return parent;
		}

		/// <summary>
		/// removes a item from the tree
		/// </summary>
		/// <remarks>this method will remove all the sub items of the item</remarks>
		/// <param name="item"></param>
		public void Remove()
		{
			Parent = null;

			foreach (var child in EnumerateChildren())
			{
				child.Parent = null;
				Children.Remove(child);
			}
		}

		/// <summary>
		/// Enumerate all the tree items from the current item to the root of the tree
		/// </summary>
		/// <returns></returns>
		public IEnumerable<TreeItem<T>> EnumerateToRoot()
		{
			var item = this;

			while (item!.HasParent)
			{
				yield return item;

				item = item.Parent;
			}

			yield return item;
		}

		/// <summary>
		/// Enumerates the values from the current value to the root of the tree
		/// </summary>
		/// <returns>all the values from the current value to the root of the tree</returns>
		public IEnumerable<T> EnumerateValuesToRoot()
		{
			var item = this;

			while (item!.HasParent)
			{
				yield return item.Value;

				item = item.Parent;
			}

			yield return item.Value;
		}


		/// <summary>
		/// Get the amount of children recursively
		/// </summary>
		/// <returns>the amount of children recursively</returns>
		public int GetChilrenCount()
		{
			var count = Children.Count;

			foreach (var child in Children)
			{
				count += child.GetChilrenCount();
			}

			return count;
		}

		public IEnumerable<TreeItem<T>> EnumerateChildren(uint layers = UInt32.MaxValue)
		{
			foreach (var child in Children)
			{
				yield return child;
			}

			if (layers > 0)
			{
				foreach (var child in Children)
				{
					foreach (var ChildOfChild in child.EnumerateChildren(layers - 1))
					{
						yield return ChildOfChild;
					}
				}
			}
		}
	}
}