using System;
using System.Collections.Generic;

namespace FileExplorerCore.Helpers
{
	public class Tree<TTreeItem, TValue> where TTreeItem : TreeItem<TValue>
	{
		public List<TTreeItem> Children { get; }

		public bool HasChildren => Children.Count > 0;

		public TTreeItem this[int index]
		{
			get => Children[index];
			set => Children[index] = value;
		}

		public Tree(IEnumerable<TTreeItem> items)
		{
			Children = new List<TTreeItem>(items);
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

		/// <summary>
		/// removes a item from the tree
		/// </summary>
		/// <remarks>this method will remove all the sub items of the item and set the parent to null</remarks>
		/// <param name="item"></param>
		public void Remove(TTreeItem item)
		{
			item.Remove();
			Children.Remove(item);
		}

		public void Clear()
		{
			foreach (var child in Children)
			{
				child.Remove();
			}

			Children.Clear();
		}

		/// <summary>
		/// Enumerates all the items in the tree
		/// </summary>
		/// <remarks>if you want to know how much items are in the tree use the <see cref="GetChilrenCount"/> method</remarks>
		/// <returns></returns>
		public IEnumerable<TTreeItem> EnumerateChilren(uint layers = UInt32.MaxValue)
		{
			foreach (var child in Children)
			{
				yield return child;
			}

			if (layers > 0)
			{
				foreach (var child in Children)
				{
					foreach (var ChildOfChild in child.EnumerateChildren(layers))
					{
						yield return (TTreeItem)ChildOfChild;
					}
				}
			}
		}
	}
}