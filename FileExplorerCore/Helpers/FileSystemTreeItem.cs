using FileExplorerCore.Interfaces;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;

namespace FileExplorerCore.Helpers
{
	[ProtoContract]
	public class FileSystemTreeItem : ITreeItem<string, FileSystemTreeItem>
	{
		private static readonly EnumerationOptions options = new()
		{
			IgnoreInaccessible = true,
			RecurseSubdirectories = false,
			AttributesToSkip = FileAttributes.System | FileAttributes.Hidden,
		};

		[ProtoMember(1, DataFormat = DataFormat.Group)]
		private List<FileSystemTreeItem>? children;

		public List<FileSystemTreeItem> Children
		{
			get
			{
				if (children is null)
				{
					children = new List<FileSystemTreeItem>();

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

		public IEnumerable<FileSystemTreeItem> Query
		{
			get
			{
				if (IsFolder)
				{
					return GetPath(path => new FileSystemEnumerable<FileSystemTreeItem>(Path.GetFullPath(path.ToString()), (ref FileSystemEntry x) => new FileSystemTreeItem(x.FileName.ToString(), x.IsDirectory, this), options));
				}

				return Enumerable.Empty<FileSystemTreeItem>();
			}
		}

		[ProtoMember(3)]
		public bool IsFolder { get; init; }

		public FileSystemTreeItem? Parent { get; set; }

		[ProtoMember(2)]
		public string Value { get; set; }

		public bool HasParent => Parent is not null;
		public bool HasChildren => children is null ? Query.Any() : children.Count > 0;

		public FileSystemTreeItem this[Index index]
		{
			get => Children[index];
			set => Children[index] = value;
		}

		public FileSystemTreeItem()
		{

		}

		public FileSystemTreeItem(string name, bool isFolder, FileSystemTreeItem? parent = null)
		{
			IsFolder = isFolder;
			Value = name;
			IsFolder = isFolder;
			Parent = parent;
		}

		/// <summary>
		/// Get the root of the tree
		/// </summary>
		/// <returns>the root of the tree</returns>
		public FileSystemTreeItem GetRoot()
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

			foreach (var child in EnumerateChildrenWithoutInitialize())
			{
				child.Parent = null;
				Children.Remove(child);
			}
		}

		/// <summary>
		/// Enumerate all the tree items from the current item to the root of the tree
		/// </summary>
		/// <returns></returns>
		public IEnumerable<FileSystemTreeItem> EnumerateToRoot()
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
		public IEnumerable<string> EnumerateValuesToRoot()
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

			var currentChildrenCount = currentChildren.Count;

			for (var i = 0; i < currentChildrenCount; i++)
			{
				count += await currentChildren[i].GetChildrenCount();

				currentChildrenCount = currentChildren.Count;
			}

			return count;
		}

		public IEnumerable<FileSystemTreeItem> EnumerateChildren(uint layers = UInt32.MaxValue)
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

		public async IAsyncEnumerable<FileSystemTreeItem> EnumerateChildrenAsync(uint layers = UInt32.MaxValue)
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

		public IEnumerable<FileSystemTreeItem> EnumerateChildrenWithoutInitialize()
		{
			return children ?? Enumerable.Empty<FileSystemTreeItem>();
		}

		public T GetPath<T>(ReadOnlySpanFunc<char, T> action)
		{
			var builder = new ValueStringBuilder(stackalloc char[512]);

			if (OperatingSystem.IsWindows())
			{
				foreach (var item in EnumerateValuesToRoot())
				{
					if (!item.EndsWith('\\'))
					{
						builder.Insert(0, '\\', 1);
					}

					builder.Insert(0, item);
				}

				if (builder[^1] is '\\')
				{
					return action(builder.AsSpan(0, builder.Length));
				}
			}
			else
			{
				foreach (var item in EnumerateValuesToRoot())
				{
					if (!item.EndsWith('/'))
					{
						builder.Insert(0, '/', 1);
					}

					builder.Insert(0, item);
				}

				if (builder[^1] is '/')
				{
					return action(builder.AsSpan(0, builder.Length));
				}
			}
			
			return action(builder.AsSpan(0, builder.Length));
		}

		public T GetPath<T, TParameter>(ReadOnlySpanFunc<char, TParameter, T> action, TParameter parameter)
		{
			var builder = new ValueStringBuilder(stackalloc char[512]);

			if (OperatingSystem.IsWindows())
			{
				foreach (var item in EnumerateValuesToRoot())
				{
					if (!item.EndsWith('\\'))
					{
						builder.Insert(0, '\\', 1);
					}

					builder.Insert(0, item);
				}

				if (builder[^1] is '\\')
				{
					return action(builder.AsSpan(0, builder.Length - 1), parameter);
				}
			}
			else
			{
				foreach (var item in EnumerateValuesToRoot())
				{
					if (!item.EndsWith('/'))
					{
						builder.Insert(0, '/', 1);
					}

					builder.Insert(0, item);
				}

				if (builder[^1] is '/')
				{
					return action(builder.AsSpan(0, builder.Length - 1), parameter);
				}
			}

			return action(builder.AsSpan(0, builder.Length), parameter);
		}

		public override string ToString()
		{
			return Value;
		}
	}
}