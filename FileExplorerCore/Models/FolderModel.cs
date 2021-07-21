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

		private bool _isSelected;

		public readonly static ConcurrentStack<FolderModel> FileImageQueue = new();

		IEnumerable<FolderModel> query = Enumerable.Empty<FolderModel>();

		public event Action SelectionChanged = delegate { };

		public static FolderModel Empty { get; } = new FolderModel();

		private readonly EnumerationOptions options = new()
		{
			IgnoreInaccessible = true,
			RecurseSubdirectories = false,
			BufferSize = 16384,
		};

		public Bitmap Image
		{
			get
			{
				if (_image is null)
				{
					FileImageQueue.Push(this);

					if (imageLoadTask is null || imageLoadTask is { IsCompleted: true })
					{
						imageLoadTask = Task.Run(() =>
						{
							while (!FileImageQueue.IsEmpty)
							{
								while (FileImageQueue.TryPop(out var subject))
								{
									var img = WindowsThumbnailProvider.GetThumbnail(subject.Path, 48, 48, ThumbnailOptions.ThumbnailOnly);

									if (img is null)
									{
										img = WindowsThumbnailProvider.GetThumbnail(subject.Path, 48, 48, ThumbnailOptions.IconOnly);
									}

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

		public Task<IEnumerable<FolderModel>> SubFolders => Task.Run(() => (IEnumerable<FolderModel>)query.ToArray());

		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				_isSelected = value;
				SelectionChanged();
			}
		}

		public FolderModel(string path, string? name = null, IEnumerable<FolderModel>? subFolders = null)
		{
			Name = name ?? System.IO.Path.GetFileName(path);

			if (String.IsNullOrEmpty(Name))
			{
				Name = path;
			}

			Path = path;

			Image = WindowsThumbnailProvider.GetThumbnail(Path, 48, 48, ThumbnailOptions.BiggerSizeOk);

			query = subFolders ?? new FileSystemEnumerable<FolderModel>(path, (ref FileSystemEntry x) => new FolderModel(x.ToFullPath(), new string(x.FileName)), options)
			{
				ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory,
			};

			IsSelected = false;
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
