using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
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
	}
}