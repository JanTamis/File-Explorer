using FileExplorerCore.Interfaces;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FileExplorerCore.Helpers
{
	[ProtoContract]
	public class Tree<TTreeItem, TValue> where TTreeItem : class, ITreeItem<TValue, TTreeItem>
	{
		[ProtoMember(1)] public List<TTreeItem> Children { get; }

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
		public int GetChildrenCount()
		{
			return Children.Count + Children.Sum(child => child.GetChildrenCount());
		}

		/// <summary>
		/// Enumerates all the items in the tree
		/// </summary>
		/// <remarks>if you want to know how much items are in the tree use the <see cref="GetChildrenCount"/> method</remarks>
		/// <returns></returns>
		public IEnumerable<TTreeItem> EnumerateChildren(uint layers = UInt32.MaxValue)
		{
			var count = Children.Count;

			for (var i = 0; i < count; i++)
			{
				yield return Children[i];

				if (layers > 0)
				{
					foreach (var childOfChild in Children[i].EnumerateChildren(layers - 1))
					{
						yield return childOfChild;
					}
				}

				count = Children.Count;
			}
		}

		/// <summary>
		/// Enumerates all the items in the tree
		/// </summary>
		/// <remarks>if you want to know how much items are in the tree use the <see cref="GetChildrenCount"/> method</remarks>
		/// <returns></returns>
		public IEnumerable<TTreeItem> EnumerateChildrenUnInitialized()
		{
			var count = Children.Count;

			for (var i = 0; i < count; i++)
			{
				yield return Children[i];

				foreach (var childOfChild in Children[i].EnumerateChildren())
				{
					yield return childOfChild;
				}

				count = Children.Count;
			}
		}
	}
}