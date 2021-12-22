using System;
using Avalonia.Media.Imaging;
using FileExplorerCore.Helpers;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;

namespace FileExplorerCore.Models
{
	public class FolderModel : ReactiveObject
	{
		private Bitmap _image;
		static Task imageLoadTask;

		public readonly static ConcurrentStack<FolderModel> FileImageQueue = new();
		readonly IEnumerable<FolderModel> query = Enumerable.Empty<FolderModel>();

		private ObservableRangeCollection<FolderModel> _folders;

		public static FolderModel Empty { get; } = new FolderModel();

		private readonly EnumerationOptions options = new()
		{
			IgnoreInaccessible = true,
			RecurseSubdirectories = false,
		};

		public Bitmap Image
		{
			get
			{
				if (_image is null)
				{
					FileImageQueue.Push(this);

					if (imageLoadTask is null or { IsCompleted: true })
					{
						imageLoadTask = Task.Run(() =>
						{
							if (OperatingSystem.IsWindows())
							{
								foreach (var subject in Concurrent.AsEnumerable(FileImageQueue))
								{
									var img = WindowsThumbnailProvider.GetThumbnail(subject.Path, 48, 48, ThumbnailOptions.BiggerSizeOk);

									subject.Image = img;
								}
							}
						});
					}
				}

				return _image;
			}
			set => this.RaiseAndSetIfChanged(ref _image, value);
		}

		public string Name { get; }
		public string Path { get; }

		public ObservableRangeCollection<FolderModel> SubFolders
		{
			get
			{
				return _folders ??= new(query);
			}
		}

		//public Task<IEnumerable<FolderModel>> SubFolders => Task.Run(() => (IEnumerable<FolderModel>)query.ToArray());

		public FolderModel(string path, string? name = null, IEnumerable<FolderModel>? subFolders = null)
		{
			Name = name ?? System.IO.Path.GetFileName(path);

			if (String.IsNullOrEmpty(Name))
			{
				Name = path;
			}

			Path = path;

			query = subFolders ?? new FileSystemEnumerable<FolderModel>(path, (ref FileSystemEntry x) => new FolderModel(x.ToFullPath(), new string(x.FileName)), options)
			{
				ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory,
			};
		}

		public FolderModel()
		{
			Path = String.Empty;
		}

		public FolderModel(string name, IEnumerable<FolderModel> subFolders) : this(String.Empty, name, subFolders)
		{

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

		public int GetHashCode([DisallowNull] FolderModel obj)
		{
			return obj.Path.GetHashCode();
		}
	}
}