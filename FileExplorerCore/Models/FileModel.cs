using FileExplorerCore.Helpers;
using FileExplorerCore.ViewModels;
using Humanizer;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;

namespace FileExplorerCore.Models
{
	public class FileModel : ViewModelBase, IDisposable
	{
		public static readonly ConcurrentBag<FileModel> FileImageQueue = new();

		public FileSystemTreeItem TreeItem { get; }

		private bool _isSelected;
		public bool NeedsNewImage = true;

		private string? _name;
		private string? _extension;
		private long _size = -1;

		private DateTime _editedOn;

		private bool _needsTranslation;

		public static event Action<FileModel> SelectionChanged = delegate { };

		private static bool _isNotLoading = true;

		public string? ExtensionName { get; set; }

		public bool IsVisible { get; set; } = true;

		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				if (_isSelected != value)
				{
					// OnPropertyChanged(ref _isSelected, value);
					SelectionChanged?.Invoke(this);
				}
			}
		}

		public bool NeedsTranslation
		{
			get => _needsTranslation;
			set => OnPropertyChanged(ref _needsTranslation, value);
		}

		public string Path
		{
			get { return TreeItem.GetPath(path => path.ToString()); }
		}

		public string Name
		{
			get => TreeItem.IsFolder ? TreeItem.Value : System.IO.Path.GetFileNameWithoutExtension(TreeItem.Value);
			set
			{
				TreeItem.Value = value;
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
						_extension = TreeItem.GetPath(path => System.IO.Path.GetExtension(path).ToString());
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
				if (_size == -1 && !IsFolder && new FileInfo(Path) is { Exists: true } info)
				{
					_size = info.Length;
				}

				return _size;
			}
		}

		public long TotalSize => IsFolder
			? TreeItem.GetPath(path => new FileSystemEnumerable<long>(path.ToString(), (ref FileSystemEntry x) => x.Length, new EnumerationOptions
			{
				RecurseSubdirectories = true,
				IgnoreInaccessible = true,
				AttributesToSkip = FileSystemTreeItem.Options.AttributesToSkip,
			})).Sum()
			: Size;

		public bool IsFolder => TreeItem.IsFolder;

		public Task<string> SizeFromTask
		{
			get
			{
				return Task.Run(() =>
				{
					var size = TreeItem.GetPath(path =>
					{
						var result = 0L;

						if (!IsFolder)
						{
							result = Size;
						}
						else if (path[^1] is '\\' && new DriveInfo(new String(path[0], 1)) is { IsReady: true } info)
						{
							result = info.TotalSize - info.TotalFreeSpace;
						}
						else if (IsFolder)
						{
							var query = new FileSystemEnumerable<long>(path.ToString(), (ref FileSystemEntry x) => x.Length,
								new EnumerationOptions { RecurseSubdirectories = true })
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
					if (!IsFolder)
					{
						_editedOn = File.GetLastWriteTime(Path);
					}
					else
					{
						_editedOn = Directory.GetLastWriteTime(Path);
					}
				}

				return _editedOn;
			}
		}

		public FileModel(FileSystemTreeItem item)
		{
			TreeItem = item;
		}

		public void Dispose()
		{
			GC.SuppressFinalize(this);
		}
	}
}