using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using FileExplorer.Interfaces;
using System.IO;
using System.IO.Enumeration;

namespace FileExplorer.Models;

/// <summary>
/// Represents a node in a file system tree.
/// </summary>
public sealed class FileSystemTreeItem(ReadOnlySpan<char> name, bool isFolder, FileSystemTreeItem? parent = null) : ITreeItem<string, FileSystemTreeItem>, IEquatable<FileSystemTreeItem>
{
	private string? _path;

	/// <summary>
	/// Enumeration options for traversing the file system.
	/// </summary>
	public static readonly EnumerationOptions Options = new()
	{
		IgnoreInaccessible = true,
		RecurseSubdirectories = false,
		AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
	};

	/// <summary>
	/// Gets the children of the current node.
	/// </summary>
	public IEnumerable<FileSystemTreeItem> Children
	{
		get
		{
			var path = GetPath();

			if (IsFolder && Directory.Exists(path))
			{
				return new FileSystemEnumerable<FileSystemTreeItem>(path, (ref FileSystemEntry x) => new FileSystemTreeItem(x.FileName, x.IsDirectory, this), Options);
			}

			return Enumerable.Empty<FileSystemTreeItem>();
		}
	}

	/// <summary>
	/// Checks if the current node has any subfolders.
	/// </summary>
	public bool HasFolders
	{
		get
		{
			var path = GetPath();

			if (IsFolder && Directory.Exists(path))
			{
				var enumerable = new FileSystemEnumerable<byte>(path, (ref FileSystemEntry _) => 0, Options)
				{
					ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory
				};

				return enumerable.Any();
			}

			return false;
		}
	}

	/// <summary>
	/// Indicates whether the current node is a folder.
	/// </summary>
	public bool IsFolder { get; } = isFolder;

	/// <summary>
	/// The parent of the current node.
	/// </summary>
	public FileSystemTreeItem? Parent { get; set; } = parent;

	/// <summary>
	/// The value of the current node.
	/// </summary>
	public string Value { get; set; } = name.ToString();

	/// <summary>
	/// Checks if the current node has a parent.
	/// </summary>
	public bool HasParent => Parent is not null;

	/// <summary>
	/// The path of the current node.
	/// </summary>
	public string Path => _path ??= GetPath();

	/// <summary>
	/// Checks if the current node has any children.
	/// </summary>
	public bool HasChildren
	{
		get
		{
			if (IsFolder && Directory.Exists(Path))
			{
				using var enumerable = new DelegateFileSystemEnumerator<byte>(Path, Options);

				return enumerable.MoveNext();
			}

			return false;
		}
	}

	/// <summary>
	/// Gets the root of the tree.
	/// </summary>
	/// <returns>The root of the tree.</returns>
	public FileSystemTreeItem GetRoot()
	{
		var parent = this;

		while (parent is { Parent: not null, })
		{
			parent = parent.Parent;
		}

		return parent;
	}

	/// <summary>
	/// Enumerates all the tree items from the current item to the root of the tree.
	/// </summary>
	/// <returns>An IEnumerable of FileSystemTreeItem from the current item to the root of the tree.</returns>
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
	/// Enumerates the values from the current value to the root of the tree.
	/// </summary>
	/// <returns>An IEnumerable of string from the current value to the root of the tree.</returns>
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
	/// Gets the amount of children recursively.
	/// </summary>
	/// <returns>The amount of children recursively.</returns>
	public int GetChildrenCount()
	{
		var options = new EnumerationOptions
		{
			IgnoreInaccessible = Options.IgnoreInaccessible,
			AttributesToSkip = Options.AttributesToSkip,
			RecurseSubdirectories = true
		};

		if (Directory.Exists(Path))
		{
			return new FileSystemEnumerable<bool>(Path, (ref FileSystemEntry _) => false, options)
				.Count();
		}

		return 0;
	}

	/// <summary>
	/// Enumerates the children of the current node recursively.
	/// </summary>
	/// <returns>An IEnumerable of FileSystemTreeItem representing the children of the current node.</returns>
	public IEnumerable<FileSystemTreeItem> EnumerateChildrenRecursive()
	{
		if (!IsFolder)
		{
			return Enumerable.Empty<FileSystemTreeItem>();
		}

		var path = _path ?? GetPath();

		if (!Directory.Exists(path))
		{
			return Enumerable.Empty<FileSystemTreeItem>();
		}

		var enumerable = new FileSystemEnumerable<FileSystemTreeItem>(path, (ref FileSystemEntry entry) => new FileSystemTreeItem(entry.FileName, entry.IsDirectory, this));

		return enumerable.SelectMany(s => s.EnumerateChildrenRecursive().Prepend(s));
	}

	/// <summary>
	/// Enumerates the children of the current node recursively asynchronously.
	/// </summary>
	/// <returns>An IAsyncEnumerable of FileSystemTreeItem array representing the children of the current node.</returns>
	public async IAsyncEnumerable<FileSystemTreeItem[]> EnumerateChildrenRecursiveAsync()
	{
		if (!IsFolder)
		{
			yield break;
		}

		var path = _path ?? GetPath();

		if (!Directory.Exists(path))
		{
			yield break;
		}

		var task = Task.Factory.StartNew(x =>
		{
			if (x is FileSystemTreeItem)
			{
				var enumerable = new FileSystemEnumerable<FileSystemTreeItem>(path, (ref FileSystemEntry entry) => new FileSystemTreeItem(entry.FileName, entry.IsDirectory, this));

				return enumerable.ToArray();
			}

			return Array.Empty<FileSystemTreeItem>();
		}, this);

		yield return await task;

		foreach (var item in task.Result)
		{
			await foreach (var child in item.EnumerateChildrenRecursiveAsync())
			{
				yield return child;
			}
		}
	}

	/// <summary>
	/// Enumerates the children of the current node recursively based on a condition.
	/// </summary>
	/// <returns>An IEnumerable of FileSystemTreeItem representing the children of the current node.</returns>
	public IEnumerable<FileSystemTreeItem> EnumerateChildrenRecursive(ReadOnlySpanFunc<char, bool> include)
	{
		return EnumerateChildren(include)
			.SelectMany(s => !s.IsFolder || include(s.Value)
				? s.EnumerateChildrenRecursive(include).Prepend(s)
				: s.EnumerateChildrenRecursive(include))
			.Where(w => !w.IsFolder || include(w.Value));
	}

	/// <summary>
	/// Enumerates the children of the current node based on a condition.
	/// </summary>
	/// <returns>An IEnumerable of FileSystemTreeItem representing the children of the current node.</returns>
	public IEnumerable<FileSystemTreeItem> EnumerateChildren(ReadOnlySpanFunc<char, bool> include)
	{
		if (!IsFolder)
		{
			return Enumerable.Empty<FileSystemTreeItem>();
		}

		var path = _path ?? GetPath();

		if (!Directory.Exists(path))
		{
			return Enumerable.Empty<FileSystemTreeItem>();
		}

		return new FileSystemTreeItemEnumerable(new DelegateFileSystemEnumerator<FileSystemTreeItem>(path, Options)
		{
			Find = (ref FileSystemEntry entry) => entry.IsDirectory || include(entry.FileName),
			Transformation = (ref FileSystemEntry entry) => new FileSystemTreeItem(entry.FileName, entry.IsDirectory, this)
		});
	}

	/// <summary>
	/// Enumerates the children of the current node.
	/// </summary>
	/// <returns>An IEnumerable of FileSystemTreeItem representing the children of the current node.</returns>
	public IEnumerable<FileSystemTreeItem> EnumerateChildren()
	{
		if (!IsFolder)
		{
			return Enumerable.Empty<FileSystemTreeItem>();
		}

		var path = _path ?? GetPath();

		if (!Directory.Exists(path))
		{
			return Enumerable.Empty<FileSystemTreeItem>();
		}

		return new FileSystemTreeItemEnumerable(new DelegateFileSystemEnumerator<FileSystemTreeItem>(path, Options)
		{
			Transformation = (ref FileSystemEntry entry) => new FileSystemTreeItem(entry.FileName, entry.IsDirectory, this)
		});
	}

	/// <summary>
	/// Gets the path of the current node.
	/// </summary>
	/// <returns>The path of the current node.</returns>
	public string GetPath()
	{
		var count = GetPathLength();

		return String.Create(count, this, BuildPath);
	}

	/// <summary>
	/// Gets the path of the current node based on a condition.
	/// </summary>
	/// <returns>The path of the current node.</returns>
	public T GetPath<T>(ReadOnlySpanFunc<char, T> action)
	{
		if (_path is not null)
		{
			return action(Path);
		}

		Span<char> buffer = stackalloc char[GetPathLength()];
		BuildPath(buffer, this);

		return action(buffer);
	}

	/// <summary>
	/// Gets the path of the current node based on a condition and a parameter.
	/// </summary>
	/// <returns>The path of the current node.</returns>
	public T GetPath<T, TParameter>(ReadOnlySpanFunc<char, TParameter, T> action, TParameter parameter)
	{
		if (_path is not null)
		{
			return action(Path, parameter);
		}

		Span<char> buffer = stackalloc char[GetPathLength()];
		BuildPath(buffer, this);

		return action(buffer, parameter);
	}

	/// <summary>
	/// Returns a string that represents the current object.
	/// </summary>
	/// <returns>A string that represents the current object.</returns>
	public override string ToString()
	{
		return Value;
	}

	/// <summary>
	/// Determines whether the specified object is equal to the current object.
	/// </summary>
	/// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
	public override bool Equals(object? obj)
	{
		return obj is FileSystemTreeItem item && Equals(item);
	}

	/// <summary>
	/// Determines whether the specified FileSystemTreeItem is equal to the current FileSystemTreeItem.
	/// </summary>
	/// <returns>true if the specified FileSystemTreeItem is equal to the current FileSystemTreeItem; otherwise, false.</returns>
	public bool Equals(FileSystemTreeItem? other)
	{
		return String.Equals(Path, other?.Path);
	}

	/// <summary>
	/// Serves as the default hash function.
	/// </summary>
	/// <returns>A hash code for the current object.</returns>
	public override int GetHashCode()
	{
		return Path.GetHashCode();
	}

	/// <summary>
	/// Creates a FileSystemTreeItem from a path.
	/// </summary>
	/// <returns>A FileSystemTreeItem that represents the path.</returns>
	public static FileSystemTreeItem? FromPath([NotNullIfNotNull(nameof(path))] string? path)
	{
		if (path is null)
		{
			return null;
		}

		var directory = new DirectoryInfo(path);

		var result = new FileSystemTreeItem(directory.Name, Directory.Exists(path));
		var temp = result;

		while (directory.Parent is not null)
		{
			directory = directory.Parent;
			temp.Parent = new FileSystemTreeItem(directory.Name, true);
			temp = temp.Parent;
		}

		return result;
	}

	/// <summary>
	/// Gets the length of the path of the current node.
	/// </summary>
	/// <returns>The length of the path of the current node.</returns>
	private int GetPathLength()
	{
		var item = this;
		var count = Value.Length;

		while (item.Parent is not null)
		{
			item = item.Parent;

			count += item.Value.Length;

			if (!item.Value.EndsWith(PathHelper.DirectorySeparator))
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Builds the path of the current node.
	/// </summary>
	private static void BuildPath(Span<char> buffer, FileSystemTreeItem item)
	{
		item.Value.CopyTo(buffer.Slice(buffer.Length - item.Value.Length));

		while (item.Parent is not null)
		{
			item = item.Parent;

			var index = buffer.LastIndexOf('\0');

			if (item.IsFolder && !item.Value.EndsWith(PathHelper.DirectorySeparator))
			{
				buffer[index] = PathHelper.DirectorySeparator;
			}

			item.Value.CopyTo(buffer.Slice(Math.Max(index - item.Value.Length, 0)));
		}
	}

	/// <summary>
	/// Represents an enumerable of FileSystemTreeItem.
	/// </summary>
	public sealed class FileSystemTreeItemEnumerable(DelegateFileSystemEnumerator<FileSystemTreeItem> enumerator) : IEnumerable<FileSystemTreeItem>
	{
		/// <summary>
		/// Returns an enumerator that iterates through the collection.
		/// </summary>
		/// <returns>An enumerator that can be used to iterate through the collection.</returns>
		public IEnumerator<FileSystemTreeItem> GetEnumerator()
		{
			return enumerator;
		}

		/// <summary>
		/// Returns an enumerator that iterates through a collection.
		/// </summary>
		/// <returns>An IEnumerator object that can be used to iterate through the collection.</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}