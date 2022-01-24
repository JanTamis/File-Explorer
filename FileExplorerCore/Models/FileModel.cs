﻿using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FileExplorerCore.Converters;
using FileExplorerCore.Helpers;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerCore.Models
{
	public class FileModel : INotifyPropertyChanged, IDisposable
	{
		public static readonly ConcurrentBag<FileModel> FileImageQueue = new();

		private byte[] _path;
		private bool isAscii;

		private bool _isSelected;
		public bool NeedsNewImage = true;

		private string? _name;
		private string? _extension;
		private long _size = -2;

		private DateTime _editedOn;

		private Bitmap? _image;
		private Transform _imageTransform;
		private bool needsTranslation;

		public static event Action<FileModel> SelectionChanged = delegate { };
		public event PropertyChangedEventHandler? PropertyChanged;

		private static bool _isNotLoading = true;
		private bool _isVisible = true;

		public static int ImageSize { get; set; }

		public string? ExtensionName { get; set; }

		public bool IsVisible
		{
			get => _isVisible;
			set
			{
				_isVisible = value;

				if (value)
				{
					NeedsNewImage = true;

					OnPropertyChanged(nameof(Image));
				}
				else
				{
					NeedsNewImage = false;

					_image?.Dispose();
					Image = null;

					NeedsNewImage = true;
				}
			}
		}

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

		public bool HasImage => _image is not null;

		public bool NeedsTranslation
		{
			get => needsTranslation;
			set => OnPropertyChanged(ref needsTranslation, value);
		}

		public string Path
		{
			get => GetEncoding().GetString(_path);
			set
			{
				isAscii = true;

				for (var i = 0; i < value.Length; i++)
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
				if (_name is null)
				{
					GetPath(path =>
					{
						_name = IsFolder
						? new String(System.IO.Path.GetFileName(path))
						: new String(System.IO.Path.GetFileNameWithoutExtension(path));
					});
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
				catch (Exception)
				{
					// ignored
				}
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
						GetPath(path =>
						{
							_extension = new string(System.IO.Path.GetExtension(path));
						});
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
					GetPath(path => _size = OperatingSystem.IsWindows() 
						? DirectoryAlternative.GetFileSize(path)
						: File.Exists(path.ToString()) ? new FileInfo(path.ToString()).Length : -1);
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
					var size = 0L;

					GetPath(path =>
					{
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
							var query = new FileSystemEnumerable<long>(new string(path), (ref FileSystemEntry x) => x.Length,
								new EnumerationOptions() { RecurseSubdirectories = true })
							{
								ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory
							};

							size = query.Sum();
						}
					});

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
					GetPath(path => _editedOn = OperatingSystem.IsWindows() 
						? DirectoryAlternative.GetFileWriteDate(path)
						: new FileInfo(path.ToString()).LastWriteTime);
				}

				return _editedOn;
			}
		}

		public Bitmap? Image
		{
			get
			{
				if (NeedsNewImage && !HasImage)
				{
					ImageTransform = null;

					FileImageQueue.Add(this);

					if (_isNotLoading)
					{
						_isNotLoading = false;

						ThreadPool.QueueUserWorkItem(x =>
						{
							foreach (var subject in Concurrent.AsEnumerable(FileImageQueue))
							{
								if (subject.IsVisible && OperatingSystem.IsWindows())
								{
									subject.GetPath(path =>
									{
										if (OperatingSystem.IsWindows())
										{
											var img = WindowsThumbnailProvider.GetThumbnail(path, ImageSize, ImageSize, ThumbnailOptions.ThumbnailOnly | ThumbnailOptions.BiggerSizeOk) ??
																				WindowsThumbnailProvider.GetThumbnail(path, ImageSize, ImageSize, ThumbnailOptions.IconOnly | ThumbnailOptions.BiggerSizeOk);

											subject.NeedsNewImage = false;
											subject.OnPropertyChanged(ref subject._image, img, nameof(Image));
										}
									});
								}
							}
							
							_isNotLoading = true;
						});
					}
				}

				return _image;
			}
			set => OnPropertyChanged(ref _image, value);
		}

		public FileModel(ReadOnlySpan<char> path, bool isFolder)
		{
			isAscii = true;

			for (var i = 0; i < path.Length; i++)
			{
				if (!Char.IsAscii(path[i]))
				{
					isAscii = false;
					break;
				}
			}

			var encoder = GetEncoding();
			var byteCount = encoder.GetByteCount(path);

			_path = new byte[byteCount];
			encoder.GetBytes(path, _path);

			IsFolder = isFolder;
		}

		public void OnPropertyChanged<T>(ref T field, T value, [CallerMemberName] string? name = null)
		{
			field = value;

			OnPropertyChanged(name);
		}

		public Task OnPropertyChanged([CallerMemberName] string? name = null)
		{
			if (Dispatcher.UIThread.CheckAccess())
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
			}
			else
			{
				return Dispatcher.UIThread.InvokeAsync(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));
			}

			return Task.CompletedTask;
		}

		public void Dispose()
		{
			_image?.Dispose();

			GC.SuppressFinalize(this);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Encoding GetEncoding()
		{
			return isAscii ? Encoding.UTF8 : Encoding.Unicode;
		}

		private void GetPath(ReadOnlySpanAction<char> action)
		{
			var encoder = GetEncoding();
			var charCount = encoder.GetMaxCharCount(_path.Length);

			Span<char> path = stackalloc char[charCount];

			charCount = encoder.GetChars(_path, path);

			action(path[..charCount]);
		}
	}
}