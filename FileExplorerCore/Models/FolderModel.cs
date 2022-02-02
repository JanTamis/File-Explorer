using System;
using FileExplorerCore.Helpers;
using ReactiveUI;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using System.Reactive.Subjects;
using Avalonia.Svg.Skia;

namespace FileExplorerCore.Models
{
	public class FolderModel : ReactiveObject
	{
		public FileSystemTreeItem TreeItem { get; }

		private IImage _image;
		static Task imageLoadTask;

		public readonly static ConcurrentBag<FolderModel> FileImageQueue = new();
		readonly IEnumerable<FolderModel> query = Enumerable.Empty<FolderModel>();

		public string Name => TreeItem.Value;
		public string Path => TreeItem.GetPath(path => path.ToString());

		public IEnumerable<FolderModel> SubFolders => TreeItem
			.EnumerateChildren(0)
			.Cast<FileSystemTreeItem>()
			.Where(w => w.IsFolder)
			.Select(s => new FolderModel(s));

		//public Task<IEnumerable<FolderModel>> SubFolders => Task.Run(() => (IEnumerable<FolderModel>)query.ToArray());

		public FolderModel(FileSystemTreeItem item)
		{
			TreeItem = item;
		}

		public override int GetHashCode()
		{
			return Path.GetHashCode();
		}

		public override string ToString()
		{
			return Name;
		}
	}

	public struct FolderModelEqualityComparer : IEqualityComparer<FolderModel>
	{
		public bool Equals(FolderModel x, FolderModel y)
		{
			return x.Path == y.Path;
		}

		public int GetHashCode(FolderModel obj)
		{
			return obj.Path.GetHashCode();
		}
	}
}