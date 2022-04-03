﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
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
						var enumerable = new FileSystemEnumerable<byte>(path, (ref FileSystemEntry _) => 0, Options);

						return enumerable
							.GetEnumerator()
							.MoveNext();
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

			if (layers is 0)
			{
				foreach (var child in Children)
				{
					yield return child;
				}

				yield break;
			}

			ArrayPoolList<FileSystemTreeItem> children = default;
			var isPoolSet = false;

			foreach (var child in Children)
			{
				yield return child;

				if (child.IsFolder)
				{
					if (!isPoolSet)
					{
						children = new ArrayPoolList<FileSystemTreeItem>(1024);
						isPoolSet = true;
					}

					children.Add(child);
				}
			}

			if (isPoolSet)
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

			var enumerable = Children;

			if (layers is 0)
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
			var builder = new ValueStringBuilder(stackalloc char[256]);

			GetPath(ref builder);

			var span = builder.AsSpan(true);
			var index = span.IndexOf('\0');

      if (index is not -1)
      {
        span = span[..index]; 
      }

			//if (OperatingSystem.IsWindows() && span.Length > 3 && span[^1] == PathHelper.DirectorySeparator)
			//{
			//	return action(span[0..^1]);
			//}
			//else if (builder.Length > 1 && span[^1] == PathHelper.DirectorySeparator)
			//{
			//	return action(span[..(builder.Length - 1)]);
			//}

			return action(span);
		}

		public T GetPath<T, TParameter>(ReadOnlySpanFunc<char, TParameter, T> action, TParameter parameter)
		{
			var builder = new ValueStringBuilder(stackalloc char[256]);

			GetPath(ref builder);

			return action(builder.AsSpan(true), parameter);
		}

		private void GetPath(ref ValueStringBuilder builder)
		{
			foreach (var item in EnumerateToRoot().Reverse())
			{
				item.DynamicString.CopyToSpan(builder.AppendSpan(item.Value.Length));

				if (builder.Length > 0 && builder[^1] != PathHelper.DirectorySeparator)
				{
					builder.Append(PathHelper.DirectorySeparator);
				}
			}

			if (builder.Length > 0 && builder[^1] != PathHelper.DirectorySeparator)
			{
				builder.Append(PathHelper.DirectorySeparator);
			}
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