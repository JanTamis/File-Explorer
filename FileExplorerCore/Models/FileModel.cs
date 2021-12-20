using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FileExplorerCore.Converters;
using FileExplorerCore.Helpers;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace FileExplorerCore.Models
{
	public class FileModel : INotifyPropertyChanged, IDisposable
	{
		public readonly static ConcurrentStack<FileModel> FileImageQueue = new();

		private byte[] _path;
		private bool isAscii;

		static Task? imageLoadTask;

		private bool _isSelected;
		public bool NeedsNewImage = true;

		private string _name;
		private string? _extension;
		private long _size = -2;

		private DateTime _editedOn;

		private Bitmap _image;
		private Transform _imageTransform;
		private static int imageSize;
		private bool needsTranslation;

		public static event Action<FileModel> SelectionChanged = delegate { };
		public event PropertyChangedEventHandler? PropertyChanged;

		public static ActionBlock<FileModel> ActionBlock;
		private bool isNotLoading = true;

		static FileModel()
		{
			ActionBlock = new ActionBlock<FileModel>(subject =>
			{
				var encoder = subject.GetEncoding();
				var charCount = encoder.GetCharCount(subject._path);

				Span<char> path = stackalloc char[charCount];

				encoder.GetChars(subject._path, path);

				var img = WindowsThumbnailProvider.GetThumbnail(path, ImageSize, ImageSize, ThumbnailOptions.ThumbnailOnly | ThumbnailOptions.BiggerSizeOk);

				if (img is null)
				{
					img = WindowsThumbnailProvider.GetThumbnail(path, ImageSize, ImageSize, ThumbnailOptions.IconOnly | ThumbnailOptions.BiggerSizeOk);

					if (img is not null)
					{
						Dispatcher.UIThread.InvokeAsync(() => subject.ImageTransform = new ScaleTransform(1, -1)).Wait();
					}
				}
				else if (ImageSize <= 64)
				{
					Dispatcher.UIThread.InvokeAsync(() => subject.ImageTransform = new ScaleTransform(1, -1)).Wait();
				}

				subject.NeedsNewImage = false;
				subject.Image = img;
			}, new ExecutionDataflowBlockOptions
			{
				EnsureOrdered = true,
				MaxDegreeOfParallelism = (int)Math.Log2(Environment.ProcessorCount)
			});
		}

		public static int ImageSize
		{
			get => imageSize;
			set => imageSize = value;
		}

		public string ExtensionName { get; set; }

		public bool IsVisible { get; set; }

		public Transform ImageTransform
		{
			get => _imageTransform;
			set => OnPropertyChanged(ref _imageTransform, value);
		}

		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				if (_isSelected != value)
				{
					OnPropertyChanged(ref _isSelected, value);
					SelectionChanged?.Invoke(this);
				}
			}
		}

		public bool HasImage => _image != null;

		public bool NeedsTranslation
		{
			get => needsTranslation;
			set => OnPropertyChanged(ref needsTranslation, value);
		}

		public string Path
		{
			get
			{
				return GetEncoding().GetString(_path);
			}
			set
			{
				isAscii = true;

				for (int i = 0; i < value.Length; i++)
				{
					if (!Char.IsAscii(value[i]))
					{
						isAscii = false;
						break;
					}
				}

				_path = GetEncoding().GetBytes(value);

				OnPropertyChanged(nameof(Name));
				OnPropertyChanged(nameof(Extension));
			}
		}

		public string Name
		{
			get
			{
				if (_name == null)
				{
					if (IsFolder)
					{
						var encoder = GetEncoding();
						var charCount = encoder.GetCharCount(_path);

						Span<char> path = stackalloc char[charCount];

						encoder.GetChars(_path, path);

						if (path.Length > 0)
						{
							if (path[^1] == '\\')
							{
								_name = Path;
							}
							else
							{
								_name = new String(System.IO.Path.GetFileName(path));
							}
						}
					}
					else
					{
						_name = new String(System.IO.Path.GetFileNameWithoutExtension(DirectoryAlternative.GetName(_path, isAscii)));
					}
				}

				return _name!;
			}
			set
			{
				try
				{
					var path = Path;

					if (!IsFolder)
					{
						var name = System.IO.Path.GetFileNameWithoutExtension(path);
						var extension = Extension;
						var newPath = path.Replace(name + extension, value + extension);

						File.Move(path, newPath);

						Path = newPath;
					}
					else if (Directory.Exists(path))
					{
						var name = System.IO.Path.GetFileNameWithoutExtension(path);
						var newPath = path.Replace(name, value);

						Directory.Move(path, newPath);

						Path = newPath;
					}

					OnPropertyChanged(ref _name, value);
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
					if (!IsFolder)
					{
						var encoder = GetEncoding();
						var charCount = encoder.GetCharCount(_path);

						Span<char> path = stackalloc char[charCount];

						encoder.GetChars(_path, path);

						_extension = new string(System.IO.Path.GetExtension(path));
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
				if (_size == -2)
				{
					_size = DirectoryAlternative.GetFileSize(_path, isAscii);
				}

				return _size;
			}
		}

		public bool IsFolder { get; init; }

		public Task<string> SizeFromTask
		{
			get
			{
				return Task.Run(() =>
				{
					var encoder = GetEncoding();
					var charCount = encoder.GetCharCount(_path);

					Span<char> path = stackalloc char[charCount];

					encoder.GetChars(_path, path);

					var size = 0L;

					if (!IsFolder)
					{
						size = Size;
					}
					else if (path[^1] == '\\' && new DriveInfo(new String(path[0], 1)) is { IsReady: true } info)
					{
						size = info.TotalSize - info.TotalFreeSpace;
					}
					else if (IsFolder)
					{
						var query = new FileSystemEnumerable<long>(new string(path), (ref FileSystemEntry x) => x.Length, new EnumerationOptions() { RecurseSubdirectories = true })
						{
							ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory
						};

						size = query.Sum();
					}

					return SizeConverter.ByteSize(size);
				});
			}
		}

		public DateTime EditedOn
		{
			get
			{
				if (_editedOn == default)
				{
					_editedOn = DirectoryAlternative.GetFileWriteDate(_path, isAscii);
				}

				return _editedOn;
			}
		}

		public Bitmap? Image
		{
			get
			{
				if (NeedsNewImage)
				{
					ImageTransform = null;

					FileImageQueue.Push(this);

					if (isNotLoading)
					{
						isNotLoading = false;

						while (FileImageQueue.TryPop(out var item) || !FileImageQueue.IsEmpty)
						{
							ActionBlock.Post(item!);
						}

						isNotLoading = true;
					}
				}

				return _image;
			}
			set => OnPropertyChanged(ref _image, value);
		}

		public FileModel(string path, bool isFolder)
		{
			Path = path;
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

		public void Dispose()
		{
			_image?.Dispose();

			GC.SuppressFinalize(this);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[NotNull]
		private Encoding GetEncoding()
		{
			return isAscii ? Encoding.ASCII : Encoding.Unicode;
		}
	}

	public enum FileType
	{
		File,
		Directory,
		Drive
	}
}