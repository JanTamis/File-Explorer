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

		public string Name => TreeItem.Value;
		public string Path => TreeItem.GetPath(path => path.ToString());

		public IEnumerable<FolderModel> SubFolders => TreeItem
			.Children
			.Where(w => w.IsFolder)
			.OrderBy(o => o.Value)
			.Select(s => new FolderModel(s));

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