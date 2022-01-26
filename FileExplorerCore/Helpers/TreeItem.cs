using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileExplorerCore.Helpers
{
	public class TreeItem<T>
	{
		protected IEnumerable<TreeItem<T>> Query;
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
							try
							{
								children.Add(item);
							}
							catch (Exception e)
							{
							}
						}
					}
					catch (Exception e)
					{
					}
				}

				return children;
			}
		}

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
		public async Task Remove()
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
		public async ValueTask<int> GetChildrenCount()
		{
			var currentChildren = children ?? await Task.Run(() => Children);
			var count = currentChildren.Count;

			foreach (var child in currentChildren)
			{
				foreach (var _ in child.EnumerateChildren())
				{
					count++;
				}
			}

			return count;
		}

		public IEnumerable<TreeItem<T>> EnumerateChildren(uint layers = UInt32.MaxValue)
		{
			var enumerable = children ?? Query;

			if (children is null)
			{
				children = new List<TreeItem<T>>();
			}

			foreach (var child in enumerable)
			{
				yield return child;

				if (children.Contains(child))
				{
					children.Add(child);
				}

				// if (!Equals(enumerable, children))
				// {
				// 	children.Add(child);
				// }

				if (layers > 0)
				{
					foreach (var childOfChild in child.EnumerateChildren(layers - 1))
					{
						yield return childOfChild;
					}
				}
			}
		}
	}
}