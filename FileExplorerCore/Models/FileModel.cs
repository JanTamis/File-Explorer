using Avalonia.Media;
using FileExplorerCore.Helpers;
using FileExplorerCore.ViewModels;
using Humanizer;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerCore.Models
{
	public class FileModel : ViewModelBase, IDisposable
	{
		public static readonly ConcurrentBag<FileModel> FileImageQueue = new();

		FileSystemTreeItem treeItem;

		private bool _isSelected;
		public bool NeedsNewImage = true;

		private string? _name;
		private string? _extension;
		private long _size = -1;

		private DateTime _editedOn;

		private IImage? _image;
		private Transform _imageTransform;
		private bool needsTranslation;

		public static event Action<FileModel> SelectionChanged = delegate { };

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
			get
			{
				return treeItem.GetPath(path => path.ToString());
			}
		}

		public string Name
		{
			get
			{
				return treeItem.IsFolder
					? treeItem.Value
					: System.IO.Path.GetFileNameWithoutExtension(treeItem.Value);
			}
			set
			{
				treeItem.Value = value;
				try
				{
					var path = Path;

					if (!IsFolder)
					{
						var name = System.IO.Path.GetFileNameWithoutExtension(path);
						var extension = Extension;
						var newPath = path.Replace(name + extension, value + extension);

						File.Move(path, newPath);
					}
					else if (Directory.Exists(path))
					{
						var name = System.IO.Path.GetFileNameWithoutExtension(path);
						var newPath = path.Replace(name, value);

						Directory.Move(path, newPath);
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
						_extension = treeItem.GetPath(path => System.IO.Path.GetExtension(path).ToString());
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
				if (_size == -1 && !IsFolder)
				{
					//if (OperatingSystem.IsWindows())
					//{
					//	_size = treeItem.GetPath(path => DirectoryAlternative.GetFileSize(path));
					//}
					//else
					//{
					var path = Path;
					_size = File.Exists(path) ? new FileInfo(path).Length : -1;
					//}
				}

				return _size;
			}
		}

		public bool IsFolder => treeItem.IsFolder;

		public Task<string> SizeFromTask
		{
			get
			{
				return Task.Run(() =>
				{
					var size = treeItem.GetPath(path =>
					{
						var result = 0L;

						if (!IsFolder)
						{
							result = Size;
						}
						else if (path[^1] == '\\' && new DriveInfo(new String(path[0], 1)) is { IsReady: true } info)
						{
							result = info.TotalSize - info.TotalFreeSpace;
						}
						else if (IsFolder)
						{
							var query = new FileSystemEnumerable<long>(path.ToString(), (ref FileSystemEntry x) => x.Length,
								new EnumerationOptions() { RecurseSubdirectories = true })
							{
								ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory
							};

							result = query.Sum();
						}

						return result;
					});

					return size.Bytes().ToString();
				});
			}
		}

		public DateTime EditedOn
		{
			get
			{
				if (_editedOn == default)
				{
					var path = Path;
					_editedOn = OperatingSystem.IsWindows()
						? DirectoryAlternative.GetFileWriteDate(path)
						: new FileInfo(path).LastWriteTime;
				}

				return _editedOn;
			}
		}


		public IImage? Image
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

						ThreadPool.QueueUserWorkItem(async x =>
						{
							while (FileImageQueue.TryTake(out var subject))
							{
								if (subject.IsVisible)
								{
									var img = await ThumbnailProvider.GetFileImage(subject.treeItem);

									subject.NeedsNewImage = false;
									await subject.OnPropertyChanged(ref subject._image, img, nameof(Image));
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

		public FileModel(FileSystemTreeItem item)
		{
			treeItem = item;
		}

		public void Dispose()
		{
			GC.SuppressFinalize(this);
		}
	}
}