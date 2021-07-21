using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FileExplorerCore.Converters;
using FileExplorerCore.Helpers;
using NetFabric.Hyperlinq;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FileExplorerCore.Models
{
	public class FileModel : INotifyPropertyChanged
	{
		public readonly static ConcurrentStack<FileModel> FileImageQueue = new();

		public static string BasePath { get; set; }

		static Task? imageLoadTask;

		private bool _isSelected;
		private bool needsNewImage;

		private string _name;
		private string _relativePath;
		private string? _extension;
		private long? _size;

		private DateTime _editedOn;

		private Bitmap _image;
		private Transform _imageTransform;
		private int imageSize;

		public event Action<FileModel> SelectionChanged;
		public event PropertyChangedEventHandler? PropertyChanged;

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
			set => this.OnPropertyChanged(ref _imageTransform, value);
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

		public string Path => System.IO.Path.Combine(BasePath, _relativePath);

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
					else if (!IsFolder)
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
					if (!IsFolder)
					{
						var name = System.IO.Path.GetFileNameWithoutExtension(Path);
						var extension = Extension;
						var newPath = Path.Replace(name + extension, value + extension);

						File.Move(Path, newPath);

						//Path = newPath;
					}
					else if (Directory.Exists(Path))
					{
						var name = System.IO.Path.GetFileNameWithoutExtension(Path);
						var newPath = Path.Replace(name, value);

						Directory.Move(Path, newPath);

						//Path = newPath;
					}

					this.OnPropertyChanged(ref _name, value);
				}
				catch (Exception) { }
			}
		}

		public string Extension
		{
			get
			{
				if (_extension is null)
				{
					var span = Path.AsSpan();

					if (!IsFolder)
					{
						_extension = System.IO.Path.GetExtension(span)
																			 .ToString();
					}

					_extension = String.Empty;
				}

				return _extension;
			}
		}

		public long Size
		{
			get
			{
				if (!_size.HasValue)
				{
					_size = DirectoryAlternative.GetFileSize(Path);
				}

				return _size.Value;
			}
		}

		public bool IsFolder { get; init; }

		public Task<string> SizeFromTask
		{
			get
			{
				return Task.Run(() =>
				{
					long size = 0;

					if (!IsFolder)
					{
						size = Size;
					}
					else if (Path.EndsWith("\\") && new DriveInfo(Path[0].ToString()) is { IsReady: true } info)
					{
						size = info.TotalSize - info.TotalFreeSpace;
					}
					else if (IsFolder)
					{
						var query = new FileSystemEnumerable<long>(Path, (ref FileSystemEntry x) => x.Length, new EnumerationOptions() { RecurseSubdirectories = true })
						{
							ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory
						};

						size = query.AsValueEnumerable()
												.Sum();
					}

					return SizeConverter.ByteSize(size);
				});
			}
		}

		public DateTime EditedOn
		{
			get
			{
				if (_editedOn == DateTime.MinValue)
				{
					_editedOn = DirectoryAlternative.GetFileWriteDate(this);
				}

				return _editedOn;
			}
		}

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
			set => this.OnPropertyChanged(ref _image, value);
		}

		public FileModel(ReadOnlySpan<char> path, ReadOnlySpan<char> filename, bool isFolder, int imageSize = 32)
		{
			_relativePath = System.IO.Path.Join(path, filename);

			SelectionChanged = delegate { };

			ImageSize = imageSize;
			IsFolder = isFolder;
		}

		public void OnPropertyChanged<T>(ref T field, T value, [CallerMemberName] string name = null)
		{
			field = value;

			OnPropertyChanged(name);
		}

		public void OnPropertyChanged([CallerMemberName] string name = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}

	public enum FileType
	{
		File,
		Directory,
		Drive
	}
}