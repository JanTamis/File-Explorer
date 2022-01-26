using ProtoBuf;
using System;
using System.IO;
using System.IO.Enumeration;
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

		public FileSystemTreeItem() : base()
		{

		}

		public FileSystemTreeItem(string path, FileSystemTreeItem parent = null) : base(Path.GetFileName(path), parent: parent)
		{
			if (Directory.Exists(path))
			{
				Query = new FileSystemEnumerable<FileSystemTreeItem>(path, (ref FileSystemEntry x) => new FileSystemTreeItem(x.ToFullPath(), this), options);
			}
		}
	}
}