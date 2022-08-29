using Humanizer;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Enumeration;
using CommunityToolkit.Mvvm.ComponentModel;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;

namespace FileExplorer.Models;

[INotifyPropertyChanged]
public partial class FileModel : IFileItem
{
	public static readonly ConcurrentBag<FileModel> FileImageQueue = new();

	public FileSystemTreeItem TreeItem { get; }

	private string? _name;
	private string? _extension;
	private long _size = -1;

	private DateTime _editedOn;

	[ObservableProperty]
	private bool _isVisible = true;

	[ObservableProperty]
	private bool _isSelected;

	public string? ExtensionName { get; set; }

	public bool IsRoot => TreeItem.HasParent;

	public string Path => TreeItem.GetPath(path => path.ToString());

	public string Name
	{
		get => TreeItem.IsFolder
			? TreeItem.Value
			: TreeItem.GetPath(path => System.IO.Path.GetFileNameWithoutExtension(path).ToString());
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
			}
			catch (Exception)
			{
				// ignored
			}

			SetProperty(ref _name, value);
		}
	}

	public string Extension => _extension ??= !IsFolder
		? TreeItem.GetPath(path => System.IO.Path.GetExtension(path).ToString())
		: String.Empty;

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

	public Task<string> SizeFromTask => Task.Run(() =>
	{
		var size = TreeItem.GetPath((path, state) =>
		{
			var result = 0L;

			if (!state.IsFolder)
			{
				result = state.Size;
			}
			else if (path[^1] == PathHelper.DirectorySeparator && new DriveInfo(new String(path[0], 1)) is { IsReady: true } info)
			{
				result = info.TotalSize - info.TotalFreeSpace;
			}
			else if (state.IsFolder)
			{
				var query = new FileSystemEnumerable<long>(path.ToString(), (ref FileSystemEntry x) => x.Length,
					new EnumerationOptions { RecurseSubdirectories = true })
				{
					ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory,
				};

				result = query.Sum();
			}

			return result;
		}, this);

		return size.Bytes().ToString();
	});

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


public T GetPath<T>(ReadOnlySpanFunc<char, T> action)
  {
	  return TreeItem.GetPath(action);
  }

  public T GetPath<T, TParameter>(ReadOnlySpanFunc<char, TParameter, T> action, TParameter parameter)
  {
	  return TreeItem.GetPath(action, parameter);
  }
}