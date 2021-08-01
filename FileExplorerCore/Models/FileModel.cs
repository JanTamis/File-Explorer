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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FileExplorerCore.Models
{
	public class FileModel : INotifyPropertyChanged
	{
		public readonly static ConcurrentStack<FileModel> FileImageQueue = new();

		private byte[] _path;
		private bool isAscii;

		static Task? imageLoadTask;

		private bool _isSelected;
		private bool needsNewImage;

		private string _name;
		private string? _extension;
		private long _size = -2;

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

		public bool HasImage => _image != null;

		public string Path
		{
			get
			{
				if (isAscii)
					return Encoding.ASCII.GetString(_path);
				else
					return Encoding.Unicode.GetString(_path);
			}
			private set
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

				if (isAscii)
					_path = Encoding.ASCII.GetBytes(value);
				else
					_path = Encoding.Unicode.GetBytes(value);
			}
		}

		public string Name
		{
			get
			{
				if (_name is null)
				{
					ReadOnlySpan<char> path;

					if (isAscii)
					{
						path = Path;
					}
					else
					{
						path = MemoryMarshal.Cast<byte, char>(_path);
					}

					if (path[^1] == '\\')
					{
						_name = Path;
					}
					else if (!IsFolder)
					{
						_name = new String(System.IO.Path.GetFileNameWithoutExtension(path));
					}
					else
					{
						_name = new String(System.IO.Path.GetFileName(path));
					}
				}

				return _name;
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
					if (!IsFolder)
					{
						ReadOnlySpan<char> path;

						if (isAscii)
						{
							path = Path;
						}
						else
						{
							path = MemoryMarshal.Cast<byte, char>(_path);
						}

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
					var path = Path;
					var size = 0L;

					if (!IsFolder)
					{
						size = Size;
					}
					else if (path.EndsWith("\\") && new DriveInfo(new string(path[0], 1)) is { IsReady: true } info)
					{
						size = info.TotalSize - info.TotalFreeSpace;
					}
					else if (IsFolder)
					{
						var query = new FileSystemEnumerable<long>(path, (ref FileSystemEntry x) => x.Length, new EnumerationOptions() { RecurseSubdirectories = true })
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
				if (_editedOn == DateTime.MinValue)
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
				if (needsNewImage)
				{
					_image = null;

					ImageTransform = null;

					FileImageQueue.Push(this);

					if (imageLoadTask is null || imageLoadTask is { IsCompleted: true })
					{
						imageLoadTask = Task.Run(() =>
						{
							while (!FileImageQueue.IsEmpty)
							{
								Concurrent.ForEach(Concurrent.AsEnumerable(FileImageQueue), async subject =>
								{
									var path = subject.Path;
									var img = WindowsThumbnailProvider.GetThumbnail(path, subject.ImageSize, subject.ImageSize, ThumbnailOptions.ThumbnailOnly | ThumbnailOptions.BiggerSizeOk);

									if (img is null)
									{
										img = WindowsThumbnailProvider.GetThumbnail(path, subject.ImageSize, subject.ImageSize, ThumbnailOptions.IconOnly | ThumbnailOptions.BiggerSizeOk);

										if (img is not null)
										{
											await Dispatcher.UIThread.InvokeAsync(() => subject.ImageTransform = new ScaleTransform(1, -1), DispatcherPriority.MaxValue);
										}
									}
									else if (ImageSize <= 64)
									{
										await Dispatcher.UIThread.InvokeAsync(() => subject.ImageTransform = new ScaleTransform(1, -1), DispatcherPriority.MaxValue);
									}

									subject.Image = img;
									subject.needsNewImage = false;
								}, Environment.ProcessorCount / 4);
							}
						});
					}
				}

				return _image;
			}
			set => this.OnPropertyChanged(ref _image, value);
		}

		public FileModel(string path, bool isFolder, int imageSize = 32)
		{
			Path = path;

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