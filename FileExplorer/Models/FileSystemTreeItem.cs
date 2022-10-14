using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using FileExplorer.Interfaces;
using System.Collections;
using System.IO;
using System.IO.Enumeration;
using System.Text;
using System.Text.Unicode;

namespace FileExplorer.Models;

public sealed class FileSystemTreeItem : ITreeItem<string, FileSystemTreeItem>, IEquatable<FileSystemTreeItem>
{
	private string? _path;

	private byte _charCount;
	private byte[] _data;

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
				if (Directory.Exists(Path))
				{
					return new FileSystemEnumerable<FileSystemTreeItem>(Path, (ref FileSystemEntry x) => new FileSystemTreeItem(x.FileName, x.IsDirectory, this), Options);
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
				if (Directory.Exists(Path))
				{
					var enumerable = new FileSystemEnumerable<byte>(Path, (ref FileSystemEntry _) => 0, Options)
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
		get
		{
			if (_path is not null)
			{
				return System.IO.Path.GetFileName(_path);
			}

			return String.Create(_charCount, _data, (buffer, data) => Utf8.ToUtf16(data, buffer, out _, out _));
		}
		set
		{
			Span<byte> buffer = stackalloc byte[value.Length * 2];

			Utf8.FromUtf16(value, buffer, out _, out var written);

			_charCount = (byte)value.Length;
			_data = buffer.Slice(0, written).ToArray();
		}
	}

	public bool HasParent => Parent is not null;

	public string Path => _path ??= GetPath();

	public bool HasChildren
	{
		get
		{
			if (IsFolder)
			{
				if (Directory.Exists(Path))
				{
					var enumerable = new DelegateFileSystemEnumerator<byte>(Path, Options);

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

		Span<byte> buffer = stackalloc byte[name.Length * 2];

		Utf8.FromUtf16(name, buffer, out _, out var written);

		_charCount = (byte)name.Length;
		_data = buffer.Slice(0, written).ToArray();
		
		// Value = name.ToString();
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

		var query = Directory.Exists(Path)
			? new FileSystemEnumerable<bool>(Path, (ref FileSystemEntry _) => false, options)
			: Enumerable.Empty<bool>();

		return query.Count();
	}

	public IEnumerable<FileSystemTreeItem> EnumerateChildren(uint layers = UInt32.MaxValue)
	{
		if (!IsFolder)
		{
			return Enumerable.Empty<FileSystemTreeItem>();
		}

		var enumerable = new FileSystemEnumerable<FileSystemTreeItem>(Path, (ref FileSystemEntry entry) => new FileSystemTreeItem(entry.FileName, entry.IsDirectory, this));

		if (layers is 0)
		{
			return enumerable;
		}

		return enumerable.SelectMany(s => s.EnumerateChildren(layers - 1).Prepend(s));
	}

	public IEnumerable<FileSystemTreeItem> EnumerateChildren(ReadOnlySpanFunc<char, bool> include, uint layers = UInt32.MaxValue)
	{
		if (!IsFolder)
		{
			return Enumerable.Empty<FileSystemTreeItem>();
		}

		var enumerable = new FileSystemTreeItemEnumerable(new DelegateFileSystemEnumerator<FileSystemTreeItem>(Path, Options)
		{
			Find = (ref FileSystemEntry entry) => entry.IsDirectory || include(entry.FileName),

			Transformation = (ref FileSystemEntry entry) => new FileSystemTreeItem(entry.FileName, entry.IsDirectory, this),
		});

		if (layers is 0)
		{
			return enumerable;
		}

		return enumerable.SelectMany(s =>
		{
			if (!s.IsFolder || include(s.Value))
			{
				return s.EnumerateChildren(include, layers - 1).Prepend(s);
			}

			return s.EnumerateChildren(include, layers - 1);
		});
	}

	private string GetPath()
	{
		var count = GetPathLength();

		return String.Create(count, this, BuildPath);
	}

	public T GetPath<T>(ReadOnlySpanFunc<char, T> action)
	{
		return action(Path);
	}

	public T GetPath<T, TParameter>(ReadOnlySpanFunc<char, TParameter, T> action, TParameter parameter)
	{
		return action(Path, parameter);
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

	private int GetPathLength()
	{
		var item = this;
		var count = _charCount;

		while (item.Parent is not null)
		{
			item = item.Parent;

			count += item._charCount;

			if (item._data[^1] != ((byte)PathHelper.DirectorySeparator))
			{
				count++;
			}
		}

		return count;
	}

	private static void BuildPath(Span<char> buffer, FileSystemTreeItem item)
	{
		Utf8.ToUtf16(item._data, buffer.Slice(buffer.Length - item.Value.Length), out _, out _);
		// item.Value.CopyTo(buffer.Slice(buffer.Length - item.Value.Length));

		while (item.Parent is not null)
		{
			item = item.Parent;

			var index = buffer.LastIndexOf('\0');

			if (item.IsFolder && !item.Value.EndsWith(PathHelper.DirectorySeparator))
			{
				buffer[index] = PathHelper.DirectorySeparator;
			}

			Utf8.ToUtf16(item._data, buffer.Slice(Math.Max(index - item.Value.Length, 0)), out _, out _);
			// item.Value.CopyTo(buffer.Slice(Math.Max(index - item.Value.Length, 0)));
		}
	}

	private class FileSystemTreeItemEnumerable : IEnumerable<FileSystemTreeItem>
	{
		private readonly FileSystemEnumerator<FileSystemTreeItem> _enumerator;

		public FileSystemTreeItemEnumerable(FileSystemEnumerator<FileSystemTreeItem> enumerator)
		{
			_enumerator = enumerator;
		}

		public IEnumerator<FileSystemTreeItem> GetEnumerator()
		{
			return _enumerator;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return _enumerator;
		}
	}
}