﻿using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace FileExplorerCore.Helpers
{
	[ProtoContract]
	[ProtoInclude(500, typeof(FileSystemTreeItem))]
	public class TreeItem<T>
	{
		public virtual IEnumerable<TreeItem<T>> Query { get; }

		[ProtoMember(1, DataFormat = DataFormat.Group)]
		private List<TreeItem<T>>? children;

		
		public List<TreeItem<T>> Children
		{
			get
			{
				if (children is null)
				{
					children = new List<TreeItem<T>>();

					try
					{
						foreach (var item in Query)
						{
							children.Add(item);
						}
					}
					catch (Exception e)
					{
					}
				}

				return children;
			}
		}

		public TreeItem<T>? Parent { get; set; }

		[ProtoMember(2)]
		public T Value { get; set; }

		public bool HasParent => Parent is not null;
		public bool HasChildren => Children.Count > 0;

		public TreeItem<T> this[int index]
		{
			get => Children[index];
			set => Children[index] = value;
		}

		public TreeItem() : this(default)
		{
		}

		public TreeItem(T value, IEnumerable<TreeItem<T>>? children = null, TreeItem<T>? parent = null)
		{
			Query = new List<TreeItem<T>>(children ?? Enumerable.Empty<TreeItem<T>>());

			Parent = parent;
			Value = value;
		}

		/// <summary>
		/// Get the root of the tree
		/// </summary>
		/// <returns>the root of the tree</returns>
		public TreeItem<T> GetRoot()
		{
			var parent = this;

			while (parent is { HasParent: true })
			{
				parent = parent.Parent;
			}

			return parent!;
		}

		/// <summary>
		/// removes a item from the tree
		/// </summary>
		/// <remarks>this method will remove all the sub items of the item</remarks>
		public void Remove()
		{
			Parent = null;

			foreach (var child in EnumerateChildren(0))
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
		public async ValueTask<int> GetChildrenCount()
		{
			var currentChildren = children ?? await Task.Run(() => Children);
			var count = currentChildren.Count;

			foreach (var child in currentChildren)
			{
				count += await child.GetChildrenCount();
			}

			return count;
		}

		public IEnumerable<TreeItem<T>> EnumerateChildren(uint layers = UInt32.MaxValue)
		{
			var count = Children.Count;
			
			for (var i = 0; i < count; i++)
			{
				yield return Children[i];

				count = Children.Count;
			}

			if (layers > 0)
			{
				for (var i = 0; i < count; i++)
				{
					foreach (var childOfChild in Children[i].EnumerateChildren(layers - 1))
					{
						yield return childOfChild;
					}
				}
			}
		}

		public async IAsyncEnumerable<TreeItem<T>> EnumerateChildrenAsync(uint layers = UInt32.MaxValue)
		{
			var items = children ?? await Task.Run(() => Children);
			var count = items.Count;

			for (var i = 0; i < count; i++)
			{
				yield return items[i];

				count = items.Count;
			}

			if (layers > 0)
			{
				for (var i = 0; i < count; i++)
				{
					await foreach (var childOfChild in Children[i].EnumerateChildrenAsync(layers - 1))
					{
						yield return childOfChild;
					}

					count = items.Count;
				}
			}
		}

		public IEnumerable<TreeItem<T>> EnumerateChildrenWitoutInitialize()
		{
			return children ?? Enumerable.Empty<TreeItem<T>>();
		}
	}
}