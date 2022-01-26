using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileExplorerCore.Helpers
{
	[ProtoContract]
	public class Tree<TTreeItem, TValue> where TTreeItem : TreeItem<TValue>
	{
		[ProtoMember(1)]
		public List<TTreeItem> Children { get; }

		public bool HasChildren => Children.Count > 0;

		public TTreeItem this[int index]
		{
			get => Children[index];
			set => Children[index] = value;
		}

		public Tree()
		{
			Children = new List<TTreeItem>();
		}

		public Tree(IEnumerable<TTreeItem> items)
		{
			Children = new List<TTreeItem>(items);
		}

		/// <summary>
		/// Get the amount of children recursively
		/// </summary>
		/// <returns>the amount of children recursively</returns>
		public async ValueTask<int> GetChildrenCount()
		{
			var count = Children.Count;

			foreach (var child in Children)
			{
				count += await child.GetChildrenCount();
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
		/// <remarks>if you want to know how much items are in the tree use the <see cref="GetChildrenCount"/> method</remarks>
		/// <returns></returns>
		public IEnumerable<TTreeItem> EnumerateChildren(uint layers = UInt32.MaxValue)
		{
			foreach (var child in Children)
			{
				yield return child;

				if (layers > 0)
				{
					foreach (var childOfChild in child.EnumerateChildren(layers - 1))
					{
						if (childOfChild is TTreeItem item)
						{
							yield return item;
						}
					}
				}
			}
		}
	}
}