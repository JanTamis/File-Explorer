using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FileExplorerCore.Converters;
using FileExplorerCore.Helpers;
using NetFabric.Hyperlinq;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;

namespace FileExplorerCore.Models
{
	public class FileModel : ReactiveObject
	{
		public readonly static ConcurrentStack<FileModel> FileImageQueue = new();
		static Task? imageLoadTask;

		private bool _isSelected;
		private bool needsNewImage = true;

		private string _name;

		private readonly Lazy<string> _extension;
		private readonly Lazy<long> _size;
		private readonly Lazy<DateTime> _editedOn;

		private Bitmap _image;
		private Transform _imageTransform;
		private int imageSize;

		public event Action<FileModel> SelectionChanged = delegate { };

		public int ImageSize
		{
			get => imageSize;
			set
			{
				needsNewImage = true;
				imageSize = value;
			}
		}

		public string ExtensionName { get; set; }

		public Transform ImageTransform
		{
			get => _imageTransform;
			set => this.RaiseAndSetIfChanged(ref _imageTransform, value);
		}

		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				_isSelected = value;
				SelectionChanged(this);
			}
		}

		public bool HasImage => _image is not null;

		public string Path { get; private set; }

		public string Name
		{
			get
			{
				if (_name is null)
				{
					var span = Path.AsSpan();

					if (span[^1] is '\\')
					{
						_name = Path;
					}
					else if (File.Exists(Path))
					{
						_name = new String(System.IO.Path.GetFileNameWithoutExtension(span));
					}
					else
					{
						_name = new String(System.IO.Path.GetFileName(span));
					}
				}

				return _name;
			}
			set
			{
				try
				{
					if (File.Exists(Path))
					{
						var name = System.IO.Path.GetFileNameWithoutExtension(Path);
						var extension = Extension;
						var newPath = Path.Replace(name + extension, value + extension);

						File.Move(Path, newPath);

						Path = newPath;
					}
					else if (Directory.Exists(Path))
					{
						var name = System.IO.Path.GetFileNameWithoutExtension(Path);
						var newPath = Path.Replace(name, value);

						Directory.Move(Path, newPath);

						Path = newPath;
					}

					this.RaiseAndSetIfChanged(ref _name, value);
				}
				catch (Exception) { }

			}
		}
		public string Extension => _extension.Value;

		public long Size => _size.Value;

		public bool IsFolder { get; init; }

		public Task<string> SizeFromTask
		{
			get
			{
				return Task.Run(() =>
				{
					long size = 0;

					if (File.Exists(Path))
					{
						size = Size;
					}
					else if (Path.EndsWith(@"\") && new DriveInfo(Path[0].ToString()) is { IsReady: true } info)
					{
						size = info.TotalSize - info.TotalFreeSpace;
					}
					else if (Directory.Exists(Path))
					{
						var query = new FileSystemEnumerable<long>(Path, (ref FileSystemEntry x) => x.Length, new EnumerationOptions() { RecurseSubdirectories = true })
						{
							ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory
						};

						size = query.AsValueEnumerable().Sum();
					}
					return SizeConverter.ByteSize(size);
				});
			}
		}
		public DateTime EditedOn => _editedOn.Value;

		public Bitmap? Image
		{
			get
			{
				if (needsNewImage)
				{
					_image?.Dispose();
					_image = null;

					ImageTransform = null;

					FileImageQueue.Push(this);

					if (imageLoadTask is null || imageLoadTask is { IsCompleted: true })
					{
						imageLoadTask = Task.Run(() =>
						{
							while (!FileImageQueue.IsEmpty)
							{
								Concurrent.ForEach(Concurrent.AsEnumerable(FileImageQueue), subject =>
								{
									var img = WindowsThumbnailProvider.GetThumbnail(subject.Path, subject.ImageSize, subject.ImageSize, ThumbnailOptions.ThumbnailOnly | ThumbnailOptions.BiggerSizeOk);

									if (img is null)
									{
										img = WindowsThumbnailProvider.GetThumbnail(subject.Path, subject.ImageSize, subject.ImageSize, ThumbnailOptions.IconOnly | ThumbnailOptions.BiggerSizeOk);

										if (img is not null)
										{
											Dispatcher.UIThread.InvokeAsync(() => subject.ImageTransform = new ScaleTransform(1, -1), DispatcherPriority.MaxValue);
										}
									}
									else if (ImageSize <= 64)
									{
										Dispatcher.UIThread.InvokeAsync(() => subject.ImageTransform = new ScaleTransform(1, -1), DispatcherPriority.MaxValue);
									}

									subject.Image = img;
									subject.needsNewImage = false;
								});
							}
						});
					}
				}

				return _image;
			}
			set => this.RaiseAndSetIfChanged(ref _image, value);
		}

		public FileModel(string path, bool isFolder, int imageSize = 32)
		{
			Path = path;
			_name = null;
			Image = null;

			this.ImageSize = imageSize;

			IsFolder = isFolder;

			_extension = new Lazy<string>(() =>
			{
				var span = path.AsSpan();

				if (File.Exists(path))
				{
					if (System.IO.Path.HasExtension(span))
					{
						return System.IO.Path.GetExtension(span).ToString();
					}
				}

				return null;
			});

			_size = new Lazy<long>(() =>
			{
				return DirectoryAlternative.GetFileSize(path);
			});

			_editedOn = new Lazy<DateTime>(() =>
			{
				return DirectoryAlternative.GetFileWriteDate(this);
			});
		}
	}

	public enum FileType
	{
		File,
		Directory,
		Drive
	}
}