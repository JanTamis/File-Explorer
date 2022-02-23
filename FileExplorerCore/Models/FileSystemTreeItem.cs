using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
using Microsoft.Toolkit.HighPerformance;
using Microsoft.Toolkit.HighPerformance.Helpers;
using ProtoBuf;

namespace FileExplorerCore.Models
{
	[ProtoContract]
	public class FileSystemTreeItem : ITreeItem<string, FileSystemTreeItem>
	{
		public static readonly EnumerationOptions Options = new()
		{
			IgnoreInaccessible = true,
			RecurseSubdirectories = false,
			AttributesToSkip = FileAttributes.System | FileAttributes.Hidden,
		};

		private bool _isLatin1;
		private byte[] _value;

		public IEnumerable<FileSystemTreeItem> Children
		{
			get
			{
				if (IsFolder)
				{
					var path = GetPath(x => x.ToString());

					if (Directory.Exists(path))
					{
						return new FileSystemEnumerable<FileSystemTreeItem>(path, (ref FileSystemEntry x)
							=> new FileSystemTreeItem(x.FileName, x.IsDirectory, this), Options);
					}
				}

				return Enumerable.Empty<FileSystemTreeItem>();
			}
		}

		public bool IsFolder { get; }

		public FileSystemTreeItem? Parent { get; set; }

		public string Value
		{
			get
			{
				return new string(_isLatin1
					? Encoding.Latin1.GetChars(_value)
					: _value.AsSpan().Cast<byte, char>());
			}
			set
			{
				_isLatin1 = true;

				for (var i = 0; i < value.Length; i++)
				{
					if ((uint)value[i] > Byte.MaxValue)
					{
						_isLatin1 = false;
						break;
					}
				}

				_value = _isLatin1
					? Encoding.Latin1.GetBytes(value)
					: value.AsSpan().AsBytes().ToArray();
			}
		}

		public bool HasParent => Parent is not null;
		public bool HasChildren => Children.Any();

		public FileSystemTreeItem(ReadOnlySpan<char> name, bool isFolder, FileSystemTreeItem? parent = null)
		{
			IsFolder = isFolder;
			Parent = parent;

			_isLatin1 = true;

			for (var i = 0; i < name.Length; i++)
			{
				if ((uint)name[i] > Byte.MaxValue)
				{
					_isLatin1 = false;
					break;
				}
			}

			if (_isLatin1)
			{
				_value = new byte[name.Length];

				Encoding.Latin1.GetBytes(name, _value);
			}
			else
			{
				_value = name.AsBytes().ToArray();
			}
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

			if (!Directory.Exists(currentPath))
			{
				yield break;
			}

			// var enumerable = new FileSystemEnumerable<FileSystemTreeItem>(currentPath, (ref FileSystemEntry x) => new FileSystemTreeItem(x.FileName, x.IsDirectory, this), Options)
			// {
			// 	ShouldIncludePredicate = findPredicate,
			// };
			var enumerable = Children;

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

			var count = GetPath(builder);

			return action(builder.RawChars[0..count]);
		}

		public T GetPath<T, TParameter>(ReadOnlySpanFunc<char, TParameter, T> action, TParameter parameter)
		{
			using var builder = new ValueStringBuilder(stackalloc char[512]);

			var count = GetPath(builder);

			return action(builder.RawChars[0..count], parameter);
		}

		private int GetPath(ValueStringBuilder builder)
		{
			foreach (var item in EnumerateToRoot().Reverse())
			{
				if (item._isLatin1)
				{
					var span = builder.AppendSpan(item._value.Length);
					Encoding.Latin1.GetChars(item._value, span);
				}
				else
				{
					builder.Append(item._value.AsSpan().Cast<byte, char>());
				}

				if (builder[^1] != PathHelper.DirectorySeparator)
				{
					builder.Append(PathHelper.DirectorySeparator);
				}
			}

			if (builder.Length > 0 && builder[^1] != PathHelper.DirectorySeparator)
			{
				builder.Append(PathHelper.DirectorySeparator);
			}

			return builder.Length;
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
}