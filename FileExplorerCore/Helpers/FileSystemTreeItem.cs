using FileExplorerCore.Interfaces;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;

namespace FileExplorerCore.Helpers
{
	[ProtoContract]
	public class FileSystemTreeItem : ITreeItem<string, FileSystemTreeItem>
	{
		public static readonly EnumerationOptions Options = new()
		{
			IgnoreInaccessible = true,
			RecurseSubdirectories = false,
			AttributesToSkip = FileAttributes.System,
		};

		public IEnumerable<FileSystemTreeItem> Children
		{
			get
			{
				return IsFolder
					? GetPath((path, parent) =>
					{
						var currentPath = path.ToString();

						return Directory.Exists(currentPath)
							? new FileSystemEnumerable<FileSystemTreeItem>(currentPath, (ref FileSystemEntry x) => new FileSystemTreeItem(x.FileName.ToString(), x.IsDirectory, parent), Options)
							: Enumerable.Empty<FileSystemTreeItem>();
					}, this)
					: Enumerable.Empty<FileSystemTreeItem>();
			}
		}

		public IEnumerable<FileSystemTreeItem> Files
		{
			get
			{
				if (!IsFolder)
				{
					return Enumerable.Empty<FileSystemTreeItem>();
				}

				return GetPath((path, parent) =>
				{
					return new FileSystemEnumerable<FileSystemTreeItem>(path.ToString(), (ref FileSystemEntry x) => new FileSystemTreeItem(x.FileName.ToString(), x.IsDirectory, parent), Options)
					{
						ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory,
					};
				}, this);
			}
		}

		public IEnumerable<FileSystemTreeItem> Folders
		{
			get
			{
				if (!IsFolder)
				{
					return Enumerable.Empty<FileSystemTreeItem>();
				}

				return GetPath((path, parent) =>
				{
					return new FileSystemEnumerable<FileSystemTreeItem>(path.ToString(), (ref FileSystemEntry x) => new FileSystemTreeItem(x.FileName.ToString(), x.IsDirectory, parent), Options)
					{
						ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory,
					};
				}, this);
			}
		}

		public bool IsFolder { get; }

		public FileSystemTreeItem? Parent { get; set; }

		public string Value { get; set; }

		public bool HasParent => Parent is not null;
		public bool HasChildren => Children.Any();

		public FileSystemTreeItem(string name, bool isFolder, FileSystemTreeItem? parent = null)
		{
			IsFolder = isFolder;
			Value = name;
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
		public int GetChildrenCount()
		{
			return EnumerateChildren().Count();
		}

		public IEnumerable<FileSystemTreeItem> EnumerateChildren(uint layers = UInt32.MaxValue)
		{
			if (!IsFolder)
			{
				yield break;
			}

			if (layers == 0)
			{
				foreach (var child in Children)
				{
					yield return child;
				}

				yield break;
			}

			var children = new List<FileSystemTreeItem>();

			foreach (var child in Children)
			{
				yield return child;

				if (child.IsFolder)
				{
					children.Add(child);
				}
			}

			foreach (var child in children)
			{
				foreach (var childOfChild in child.EnumerateChildren(layers - 1))
				{
					yield return childOfChild;
				}
			}
		}

		public IEnumerable<FileSystemTreeItem> EnumerateChildren(FileSystemEnumerable<FileSystemTreeItem>.FindPredicate findPredicate, uint layers = UInt32.MaxValue)
		{
			if (!IsFolder)
			{
				yield break;
			}

			var currentPath = GetPath(path => path.ToString());
			
			var enumerable = Directory.Exists(currentPath)
					? new FileSystemEnumerable<FileSystemTreeItem>(currentPath, (ref FileSystemEntry x) => new FileSystemTreeItem(x.FileName.ToString(), x.IsDirectory, this), Options)
					{
						ShouldIncludePredicate = findPredicate,
					}
					: Enumerable.Empty<FileSystemTreeItem>();

			if (layers == 0)
			{
				foreach (var child in enumerable)
				{
					yield return child;
				}

				yield break;
			}
			
			var children = new List<FileSystemTreeItem>();

			foreach (var child in enumerable)
			{
				yield return child;

				if (child.IsFolder)
				{
					children.Add(child);
				}
			}

			foreach (var child in children)
			{
				foreach (var childOfChild in child.EnumerateChildren(layers - 1))
				{
					yield return childOfChild;
				}
			}
		}

		public T GetPath<T>(ReadOnlySpanFunc<char, T> action)
		{
			using var builder = new ValueStringBuilder(stackalloc char[512]);

			foreach (var item in EnumerateValuesToRoot())
			{
				builder.Insert(0, item);

				if (builder[0] != PathHelper.DirectorySeparator)
				{
					builder.Insert(0, PathHelper.DirectorySeparator);
				}
			}

			if (OperatingSystem.IsWindows())
			{
				return action(builder[1..]);
			}

			return action(builder.AsSpan());
		}

		public T GetPath<T, TParameter>(ReadOnlySpanFunc<char, TParameter, T> action, TParameter parameter)
		{
			var builder = new ValueStringBuilder(stackalloc char[512]);

			foreach (var item in EnumerateValuesToRoot())
			{
				builder.Insert(0, item);

				if (builder[0] != PathHelper.DirectorySeparator)
				{
					builder.Insert(0, PathHelper.DirectorySeparator);
				}
			}

			if (OperatingSystem.IsWindows())
			{
				return action(builder[1..], parameter);
			}

			return action(builder.AsSpan(), parameter);
		}

		public override string ToString()
		{
			return Value;
		}
	}
}