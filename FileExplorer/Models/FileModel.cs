using Humanizer;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using CommunityToolkit.HighPerformance.Helpers;

namespace FileExplorer.Models;

public sealed class FileModel : IFileItem, INotifyPropertyChanged
{
	public FileSystemTreeItem TreeItem { get; }

	private string? _name;
	private string? _extension;
	private long _size = -1;

	private DateTime _editedOn;

	private bool _isVisible = true;
	private bool _isSelected;

	public bool IsVisible
	{
		get => _isVisible;
		set => SetProperty(ref _isVisible, value);
	}

	public bool IsSelected
	{
		get => _isSelected;
		set => SetProperty(ref _isSelected, value);
	}

	public string? ExtensionName { get; set; }

	public bool IsRoot => !TreeItem.HasParent;

	public string Path => TreeItem.Path;

	public string Name
	{
		get => TreeItem.IsFolder
			? TreeItem.Value
			: _name ??= System.IO.Path.GetFileNameWithoutExtension(TreeItem.Value);
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

	public string Extension =>_extension ??= !IsFolder
		? System.IO.Path.GetExtension(TreeItem.Value)
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
		? new FileSystemEnumerable<long>(Path, (ref FileSystemEntry x) => x.Length, new EnumerationOptions
			{
				RecurseSubdirectories = true,
				IgnoreInaccessible = true,
				AttributesToSkip = FileSystemTreeItem.Options.AttributesToSkip,
			}).Sum()
		: Size;

	public bool IsFolder => TreeItem.IsFolder;

	public Task<string> SizeFromTask => Task.Factory.StartNew(x =>
	{
		var file = x as FileModel;
		var path = file!.TreeItem.Path;
		var result = 0L;

		if (!file.IsFolder)
		{
			result = file.Size;
		}
		else if (path[^1] == PathHelper.DirectorySeparator && new DriveInfo(new String(path[0], 1)) is { IsReady: true } info)
		{
			result = info.TotalSize - info.TotalFreeSpace;
		}
		else if (file.IsFolder)
		{
			var query = new FileSystemEnumerable<long>(path, (ref FileSystemEntry x) => x.Length,
				new EnumerationOptions { RecurseSubdirectories = true })
				{
					ShouldIncludePredicate = (ref FileSystemEntry y) => !y.IsDirectory,
				};

			result = query.Sum();
		}

		return result.Bytes().ToString();
	}, this);

	public DateTime EditedOn
	{
		get
		{
			if (_editedOn == default)
			{
				_editedOn = IsFolder
					? Directory.GetLastWriteTime(Path)
					: File.GetLastWriteTime(Path);
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

	public override int GetHashCode()
	{
		return GetPath(HashCode<char>.Combine);
	}

	private bool SetProperty<T>([NotNullIfNotNull("newValue")] ref T field, T newValue, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, newValue))
		{
			return false;
		}

		field = newValue;
		OnPropertyChanged(propertyName);
		return true;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}