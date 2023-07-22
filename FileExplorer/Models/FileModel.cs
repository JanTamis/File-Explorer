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
using CommunityToolkit.Mvvm.ComponentModel;
using FileExplorer.Resources;

namespace FileExplorer.Models;

[INotifyPropertyChanged]
public sealed partial class FileModel : IFileItem
{
	public FileSystemTreeItem TreeItem { get; }

	private string? _name;
	private string? _extension;
	private long _size = -1;

	private DateTime _editedOn;

	[ObservableProperty]
	private bool _isVisible;
	
	[ObservableProperty]
	private bool _isSelected;


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

			SetProperty(ref _name, value);
		}
	}

	public string Extension => _extension ??= !IsFolder
		? System.IO.Path.GetExtension(TreeItem.Value)
		: String.Empty;

	public string ToolTipText
	{
		get
		{
			if (IsFolder)
			{
				return $"""
					{ResourceDefault.Name}: {Name}
					{ResourceDefault.EditDate}: {EditedOn}
					""";
			}
			
			return $"""
				{ResourceDefault.Name}: {Name}
				{ResourceDefault.Extension}: {Extension}
				{ResourceDefault.Size}: {Size.Bytes().ToString()}
				{ResourceDefault.EditDate}: {EditedOn}
				""";
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
			var query = new FileSystemEnumerable<long>(path, (ref FileSystemEntry entry) => entry.Length,
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

	public void UpdateData()
	{
		_size = -1;
		_editedOn = default;

		OnPropertyChanged(nameof(Size));
		OnPropertyChanged(nameof(EditedOn));

	}

	public string GetPath()
	{
		return TreeItem.GetPath();
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
}