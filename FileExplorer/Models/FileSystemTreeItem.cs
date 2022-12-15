using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using FileExplorer.Interfaces;
using System.IO;
using System.IO.Enumeration;

namespace FileExplorer.Models;

public sealed class FileSystemTreeItem : ITreeItem<string, FileSystemTreeItem>, IEquatable<FileSystemTreeItem>
{
	private string? _path;

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
			if (IsFolder && Directory.Exists(GetPath()))
			{
				return new FileSystemEnumerable<FileSystemTreeItem>(Path, (ref FileSystemEntry x) => new FileSystemTreeItem(x.FileName, x.IsDirectory, this), Options);
			}

			return Enumerable.Empty<FileSystemTreeItem>();
		}
	}

	public bool HasFolders
	{
		get
		{
			if (IsFolder && Directory.Exists(GetPath()))
			{
				var enumerable = new FileSystemEnumerable<byte>(Path, (ref FileSystemEntry _) => 0, Options)
				{
					ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory,
				};

				return enumerable.Any();
			}

			return false;
		}
	}

	public bool IsFolder { get; }

	public FileSystemTreeItem? Parent { get; set; }

	public string Value { get; set; }

	public bool HasParent => Parent is not null;

	public string Path => _path ??= GetPath();

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

	public FileSystemTreeItem(ReadOnlySpan<char> name, bool isFolder, FileSystemTreeItem? parent = null)
	{
		IsFolder = isFolder;
		Parent = parent;

		Value = name.ToString();
	}

	/// <summary>
	/// Get the root of the tree
	/// </summary>
	/// <returns>the root of the tree</returns>
	public FileSystemTreeItem GetRoot()
	{
		var parent = this;

		while (parent is { Parent: not null })
		{
			parent = parent.Parent;
		}

		return parent;
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

		if (Directory.Exists(Path))
		{
			return new FileSystemEnumerable<bool>(Path, (ref FileSystemEntry _) => false, options)
				.Count();
		}

		return 0;
	}

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
			if (x is FileSystemTreeItem item)
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

	public IEnumerable<FileSystemTreeItem> EnumerateChildrenRecursive(ReadOnlySpanFunc<char, bool> include)
	{
		return EnumerateChildren(include)
			.SelectMany(s =>
			{
				return !s.IsFolder || include(s.Value)
					? s.EnumerateChildrenRecursive(include).Prepend(s)
					: s.EnumerateChildrenRecursive(include);
			})
			.Where(w => !w.IsFolder || include(w.Value));
	}

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
			Transformation = (ref FileSystemEntry entry) => new FileSystemTreeItem(entry.FileName, entry.IsDirectory, this),
		});
	}

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
			Transformation = (ref FileSystemEntry entry) => new FileSystemTreeItem(entry.FileName, entry.IsDirectory, this),
		});
	}

	public string GetPath()
	{
		var count = GetPathLength();

		return String.Create(count, this, BuildPath);
	}

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

	public override string ToString()
	{
		return Value;
	}

	public override bool Equals(object? obj)
	{
		return obj is FileSystemTreeItem item && Equals(item);
	}

	public bool Equals(FileSystemTreeItem? other)
	{
		return String.Equals(Path, other?.Path);
	}

	public override int GetHashCode()
	{
		return Path.GetHashCode();
	}

	public static FileSystemTreeItem? FromPath([NotNullIfNotNull("path")] string? path)
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

	private class FileSystemTreeItemEnumerable : IEnumerable<FileSystemTreeItem>
	{
		private readonly DelegateFileSystemEnumerator<FileSystemTreeItem> _enumerator;

		public FileSystemTreeItemEnumerable(DelegateFileSystemEnumerator<FileSystemTreeItem> enumerator)
		{
			_enumerator = enumerator;
		}

		public IEnumerator<FileSystemTreeItem> GetEnumerator()
		{
			return _enumerator;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}