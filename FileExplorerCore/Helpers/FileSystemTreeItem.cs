using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Reflection.Metadata;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace FileExplorerCore.Helpers
{
	[ProtoContract]
	public class FileSystemTreeItem : TreeItem<string>
	{
		private static readonly EnumerationOptions options = new()
		{
			IgnoreInaccessible = true,
			RecurseSubdirectories = false,
			AttributesToSkip = FileAttributes.System,
		};

		public override IEnumerable<TreeItem<string>> Query
		{
			get
			{
				if (IsFolder)
				{
					var builder = new ValueStringBuilder(stackalloc char[512]);

					foreach (var item in EnumerateValuesToRoot())
					{
						builder.Insert(0, '/', 1);
						builder.Insert(0, item);
					}

					var path = builder.AsSpan(0, builder.Length - 1);

					return new FileSystemEnumerable<FileSystemTreeItem>(path.ToString(), (ref FileSystemEntry x) => new FileSystemTreeItem(x.FileName.ToString(), x.IsDirectory, this), options);
				}

				return Enumerable.Empty<FileSystemTreeItem>();
			}
		}

		[ProtoMember(3)]
		public bool IsFolder { get; init; }

		public FileSystemTreeItem()
		{

		}

		public FileSystemTreeItem(string name, bool isFolder, FileSystemTreeItem parent = null) : base(name, parent: parent)
		{
			IsFolder = isFolder;
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
					return action(builder.AsSpan(0, builder.Length - 1));
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
					return action(builder.AsSpan(0, builder.Length - 1));
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
	}
}