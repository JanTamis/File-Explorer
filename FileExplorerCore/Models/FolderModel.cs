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
		private FileSystemTreeItem _treeItem;

		private IImage _image;
		static Task imageLoadTask;

		public readonly static ConcurrentBag<FolderModel> FileImageQueue = new();
		readonly IEnumerable<FolderModel> query = Enumerable.Empty<FolderModel>();

		private ObservableRangeCollection<FolderModel>? _folders;

		private readonly EnumerationOptions options = new()
		{
			IgnoreInaccessible = true,
			RecurseSubdirectories = false,
		};

		public IImage? Image
		{
			get
			{
//if (_image is null)
//{
//	FileImageQueue.Add(this);

//	if (imageLoadTask is null or { IsCompleted: true })
//	{
//		//imageLoadTask = Task.Run(async () =>
//		//{
//		//	var attempts = 0;

//		//	while (!FileImageQueue.IsEmpty && (FileImageQueue.TryTake(out var subject) || ++attempts <= 5))
//		//	{
//		//		var img = await ThumbnailProvider.GetFileImage(subject._treeItem);

//		//		subject.Image = img;
//		//	}
//		//});
//	}
//}

				return ThumbnailProvider.GetFileImage(_treeItem);
			}
			//set => this.RaiseAndSetIfChanged(ref _image, value);
		}

		public string Name => _treeItem.Value;
		public string Path => _treeItem.GetPath(path => path.ToString());

		public IEnumerable<FolderModel> SubFolders => _treeItem
			.EnumerateChildren(0)
			.Cast<FileSystemTreeItem>()
			.Where(w => w.IsFolder)
			.Select(s => new FolderModel(s));

		//public Task<IEnumerable<FolderModel>> SubFolders => Task.Run(() => (IEnumerable<FolderModel>)query.ToArray());

		public FolderModel(FileSystemTreeItem item)
		{
			_treeItem = item;
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