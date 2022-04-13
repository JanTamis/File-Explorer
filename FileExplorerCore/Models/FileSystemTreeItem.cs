using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
using Microsoft.Toolkit.HighPerformance.Helpers;
using ProtoBuf;

namespace FileExplorerCore.Models;

//[ProtoContract]
public class FileSystemTreeItem : ITreeItem<string, FileSystemTreeItem>
{
	public static readonly EnumerationOptions Options = new()
	{
		IgnoreInaccessible = true,
		RecurseSubdirectories = false,
		AttributesToSkip = FileAttributes.System | FileAttributes.Hidden,
	};

	public IEnumerable<FileSystemTreeItem> Children
	{
		get
		{
			if (IsFolder)
			{
				var path = GetPath(x => x.ToString());

				if (Directory.Exists(path))
				{
					return new FileSystemEnumerable<FileSystemTreeItem>(path, (ref FileSystemEntry x) => new FileSystemTreeItem(x.FileName, x.IsDirectory, this), Options);
				}
			}

			return Enumerable.Empty<FileSystemTreeItem>();
		}
	}

	public bool HasFolders
	{
		get
		{
			if (IsFolder)
			{
				var path = GetPath(x => x.ToString());

				if (Directory.Exists(path))
				{
					var enumerable = new FileSystemEnumerable<byte>(path, (ref FileSystemEntry _) => 0, Options)
					{
						ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory,
					};

					return enumerable
						.GetEnumerator()
						.MoveNext();
				}
			}

			return false;
		}
	}

	public bool IsFolder { get; }

	public FileSystemTreeItem? Parent { get; set; }

	public string Value
	{
		get => DynamicString.ToString();
		set => DynamicString = new DynamicString(value);
	}

	public DynamicString DynamicString { get; set; }

	public bool HasParent => Parent is not null;

	public bool HasChildren
	{
		get
		{
			if (IsFolder)
			{
				var path = GetPath(x => x.ToString());

				if (Directory.Exists(path))
				{
					var enumerable = new DelegateFileSystemEnumerator<byte>(path, Options);

					return enumerable.MoveNext();
				}
			}

			return false;
		}
	}

	public FileSystemTreeItem(ReadOnlySpan<char> name, bool isFolder, FileSystemTreeItem? parent = null)
	{
		IsFolder = isFolder;
		Parent = parent;

		DynamicString = new DynamicString(name);
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
		var options = new EnumerationOptions
		{
			IgnoreInaccessible = Options.IgnoreInaccessible,
			AttributesToSkip = Options.AttributesToSkip,
			RecurseSubdirectories = true,
		};

		var query = IsFolder
			? GetPath((path, fileOptions) =>
			{
				var currentPath = path.ToString();

				return Directory.Exists(currentPath)
					? new FileSystemEnumerable<bool>(currentPath, (ref FileSystemEntry _) => false, fileOptions)
					: Enumerable.Empty<bool>();
			}, options)
			: Enumerable.Empty<bool>();

		return query.Count();
	}

	public IEnumerable<FileSystemTreeItem> EnumerateChildren(uint layers = UInt32.MaxValue)
	{
		if (!IsFolder)
		{
			yield break;
		}

		var enumerable = new DelegateFileSystemEnumerator<FileSystemTreeItem>(GetPath(x => x.ToString()), Options)
		{
			Transformation = (ref FileSystemEntry entry) => new FileSystemTreeItem(entry.FileName, entry.IsDirectory, this),
		};

		if (layers is 0)
		{
			foreach (var child in Children)
			{
				yield return child;
			}

			yield break;
		}

		var children = new ArrayPoolList<FileSystemTreeItem>(128);

		foreach (var child in Children)
		{
			yield return child;

			if (child.IsFolder)
			{
				children.Add(child);
			}
		}

		if (children.Count > 0)
		{
			foreach (var child in children)
			{
				if (child is not null)
				{
					foreach (var childOfChild in child.EnumerateChildren(layers - 1))
					{
						yield return childOfChild;
					}
				}
			}
		}

		children.Dispose();
	}

	public IEnumerable<FileSystemTreeItem> EnumerateChildren(FileSystemEnumerable<FileSystemTreeItem>.FindPredicate findPredicate, uint layers = UInt32.MaxValue)
	{
		if (!IsFolder)
		{
			yield break;
		}

		var path = GetPath(x => x.ToString());

		var enumerable = new DelegateFileSystemEnumerator<FileSystemTreeItem>(path, Options)
		{
			Find = findPredicate,
			Transformation = (ref FileSystemEntry entry) => new FileSystemTreeItem(entry.FileName, entry.IsDirectory, this),
		};

		if (layers is 0)
		{
			while (enumerable.MoveNext())
			{
				yield return enumerable.Current;
			}

			yield break;
		}

		var children = new List<FileSystemTreeItem>();
			
		while (enumerable.MoveNext())
		{
			yield return enumerable.Current;

			if (enumerable.Current.IsFolder)
			{
				children.Add(enumerable.Current);
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
		var builder = new ValueStringBuilder(stackalloc char[256]);

		GetPath(ref builder);

		var span = builder.AsSpan(true);

    return action(span);
	}

	public T GetPath<T, TParameter>(ReadOnlySpanFunc<char, TParameter, T> action, TParameter parameter)
	{
		var builder = new ValueStringBuilder(stackalloc char[256]);

		GetPath(ref builder);

		var span = builder.AsSpan();

		return action(span, parameter);
	}

	private void GetPath(ref ValueStringBuilder builder)
	{
		var items = EnumerateToRoot().ToArray();
		Array.Reverse(items);

    for (int i = 0; i < items.Length; i++)
    {
			items[i].DynamicString.CopyToSpan(builder.AppendSpan(items[i].Value.Length));

			if (i != items.Length - 1 && builder[^1] != PathHelper.DirectorySeparator)
			{
				builder.Append(PathHelper.DirectorySeparator);
			}
		}

		//foreach (var item in )
		//{
		//	item.DynamicString.CopyToSpan(builder.AppendSpan(item.Value.Length));

		//	if (builder.Length > 0 && builder[^1] != PathHelper.DirectorySeparator)
		//	{
		//		builder.Append(PathHelper.DirectorySeparator);
		//	}
		//}

		//if (builder.Length > 0 && builder[^1] != PathHelper.DirectorySeparator)
		//{
		//	builder.Append(PathHelper.DirectorySeparator);
		//}
	}

	public override string ToString()
	{
		return Value;
	}

	public override int GetHashCode()
	{
		return GetPath(HashCode<char>.Combine);
	}
}