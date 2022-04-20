using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using FileExplorerCore.DisplayViews;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
using FileExplorerCore.Models;
using Humanizer;

namespace FileExplorerCore.ViewModels;

public class TabItemViewModel : ViewModelBase
{
	private string _search = String.Empty;

	private int _count;
	private int _fileCount;
	private int _foundItems;
	private int _selectionCount;

	private bool _isUserEntered = true;
	private bool _isLoading;
	private bool _isSelected;

	private ViewTypes _currentViewMode = ViewTypes.List;

	private readonly Stack<FileSystemTreeItem?> _undoStack = new();
	private readonly Stack<FileSystemTreeItem?> _redoStack = new();

	public CancellationTokenSource? TokenSource;
	private Control _displayControl = new Quickstart();

	private IPopup? _popupContent;

	private DateTime _startSearchTime;

	private TimeSpan _predictedTime;
	private TimeSpan _previousLoadTime;

	private SortEnum _sort = SortEnum.None;
	private FileSystemTreeItem? _treeItem;

	public event Action? PathChanged;

	public SortEnum Sort
	{
		get => _sort;
		set
		{
			OnPropertyChanged(ref _sort, value);

			if (!IsLoading && Files.Count > 1)
			{
				Files.Sort(new FileModelComparer(Sort));
			}
			else if (TreeItem is not null)
			{
				UpdateFiles(false, "*");
			}
		}
	}

	public IEnumerable<FolderModel> Folders => TreeItem?
		.EnumerateToRoot()
		.Reverse()
		.Select(s => new FolderModel(s)) ?? Enumerable.Empty<FolderModel>();

  public bool IsSelected
  {
    get => _isSelected;
    set => OnPropertyChanged(ref _isSelected, value);
  }

  public int Count
	{
		get => _count;
		set
		{
			OnPropertyChanged(ref _count, value);
			OnPropertyChanged(nameof(FileCountText));
		}
	}

	public int FileCount
	{
		get => _fileCount;
		set
		{
			OnPropertyChanged(ref _fileCount, value);

			OnPropertyChanged(nameof(IsIndeterminate));
			OnPropertyChanged(nameof(SearchProgression));
			//OnPropertyChanged(nameof(SearchText));
		}
	}

	public string FileCountText => Count switch
	{
		1 => "1 item",
		_ => $"{Count:N0} items",
	};

	public Task<string> SelectionText => Task.Run(() =>
	{
		var result = String.Empty;
		var fileSize = -1L;

		_selectionCount = 0;

		foreach (var file in Files)
		{
			if (file.IsSelected)
			{
				_selectionCount++;

				if (!file.IsFolder)
				{
					fileSize += file.Size;
				}
			}
		}

		OnPropertyChanged(nameof(SelectionCount));
		OnPropertyChanged(nameof(HasSelection));

		if (SelectionCount > 0)
		{
			result = $"{SelectionCount:N0} items selected";

			if (fileSize is not -1)
			{
				result += $", {fileSize.Bytes()}";
			}
		}

		return result;
	});

	public int SelectionCount
	{
		get => _selectionCount;
		private set
		{
			OnPropertyChanged(ref _selectionCount, value);
			OnPropertyChanged(nameof(HasSelection));
		}
	}

	public bool HasSelection => SelectionCount > 0;

	public TimeSpan LoadTime => DateTime.Now - _startSearchTime;

	public bool IsLoading
	{
		get => _isLoading;
		set
		{
			OnPropertyChanged(ref _isLoading, value);

			if (IsLoading)
			{
				FileCount = -1;
			}

			OnPropertyChanged(nameof(SearchFailed));
		}
	}

	public bool IsIndeterminate => FileCount is -1;

	public double SearchProgression => Count / (double)FileCount;

	public string SearchText
	{
		get
		{
			if (IsIndeterminate || SearchProgression is 0 or Double.NaN)
			{
				return String.Empty;
			}

			var loadTime = LoadTime;
			_predictedTime -= TimeSpan.FromSeconds(1);

			if ((loadTime - _previousLoadTime).TotalSeconds >= 3)
			{
				var deltaItems = Count - _foundItems;
				var remainingItems = FileCount - Count;
				var elapsed = loadTime - _previousLoadTime;

				_previousLoadTime = loadTime;

				if (deltaItems > 0)
				{
					_predictedTime = TimeSpan.FromSeconds(remainingItems / (double)deltaItems * elapsed.TotalSeconds);
				}

				_foundItems = Count;
			}

			return $"Remaining Time: {_predictedTime.Humanize()}";
		}
	}

	public bool SearchFailed => !IsLoading && Files.Count is 0 && DisplayControl is not Quickstart;

	public ObservableRangeCollection<FileModel> Files { get; } = new();

	public FileSystemTreeItem? TreeItem
	{
		get => _treeItem;
		set
		{
			OnPropertyChanged(ref _treeItem, value);

			PathChanged?.Invoke();

			OnPropertyChanged(nameof(FolderName));

			if (value is null && DisplayControl is not Quickstart)
			{
				DisplayControl = new Quickstart();
			}
			else
			{
				CurrentViewMode = CurrentViewMode;
			}

			if (_isUserEntered)
			{
				_undoStack.Push(TreeItem);
				_redoStack.Clear();
			}

			OnPropertyChanged(nameof(FolderName));
			OnPropertyChanged(nameof(Folders));

			IsSearching = false;
		}
	}

	public string FolderName => TreeItem?.Value ?? String.Empty;

	public string Search
	{
		get => _search;
		set => OnPropertyChanged(ref _search, value);
	}

	public Control DisplayControl
	{
		get => _displayControl;
		set => OnPropertyChanged(ref _displayControl, value);
	}

	public bool IsSearching { get; set; }

	public ViewTypes CurrentViewMode
	{
		get => _currentViewMode;
		set
		{
			OnPropertyChanged(ref _currentViewMode, value);

			switch (value)
			{
				case ViewTypes.Grid:
					var grid = new FileGrid
					{
						Files = Files,
					};

					grid.PathChanged += async path => await SetPath(path);
					grid.SelectionChanged += count => SelectionCount = count;

					DisplayControl = grid;
					break;
				case ViewTypes.List:
					var list = new FileDataGrid
					{
						Files = Files,
					};

					list.PathChanged += async path => await SetPath(path);
					list.SelectionChanged += count => SelectionCount = count;

					DisplayControl = list;
					break;
			}
		}
	}

	public IPopup? PopupContent
	{
		get => _popupContent;
		set
		{
			OnPropertyChanged(ref _popupContent, value);

			if (PopupContent is not null)
			{
				PopupContent.OnClose += () => PopupContent = null;
			}

			OnPropertyChanged(nameof(PopupVisible));
		}
	}

	public bool PopupVisible => PopupContent is not null;

	public TabItemViewModel()
	{
		Files.CountChanged += count => Count = count;
	}

	public FileSystemTreeItem? Undo()
	{
		if (_undoStack.TryPop(out var path))
		{
			_isUserEntered = false;

			_redoStack.Push(TreeItem);
		}

		return path;
	}

	public FileSystemTreeItem? Redo()
	{
		if (_redoStack.TryPop(out var path))
		{
			_isUserEntered = false;

			_undoStack.Push(TreeItem);
		}

		return path;
	}

	public void CancelUpdateFiles()
	{
		if (TokenSource is { IsCancellationRequested: false })
		{
			IsLoading = false;
			TokenSource.Cancel();
		}
	}

	public async ValueTask UpdateFiles(bool recursive, string search)
	{
		if (TreeItem is null)
		{
			return;
		}

		foreach (var file in Files)
		{
			file.IsVisible = false;
		}

		TokenSource?.Cancel();

		TokenSource = new CancellationTokenSource();
		_previousLoadTime = TimeSpan.Zero;

		await Files.ClearTrim();

		SelectionCount = 0;
		await OnPropertyChanged(nameof(SelectionText));

		IsLoading = true;

		if (TreeItem.IsFolder)
		{
			await Task.Run(async () =>
			{
				var query = recursive
					? GetFileSystemEntriesRecursive(TreeItem, search)
					: GetFileSystemEntries(TreeItem);

				if (Sort is not SortEnum.None)
				{
					ThreadPool.QueueUserWorkItem(_ =>
					{
						var count = GetFileSystemEntriesCount(TreeItem, search, TokenSource.Token);

						if (!TokenSource.IsCancellationRequested)
						{
							FileCount = count;
						}
					});
				}

				if (recursive)
				{
					if (Sort is SortEnum.None)
					{
						await Files.AddRange<Comparer<FileModel>>(query, token: TokenSource.Token);
					}
					else
					{
						var comparer = new FileModelComparer(Sort);

						await Files.AddRange(query, comparer, token: TokenSource.Token);
					}
				}
				else
				{
					var comparer = new FileModelComparer(Sort);

					await Files.AddRange(query, comparer, token: TokenSource.Token);
				}
			});
		}

		IsLoading = false;
	}

	public async ValueTask SetPath(FileSystemTreeItem? path)
	{
		if (path is null)
		{
			TreeItem = path;
			DisplayControl = new Quickstart();

			return;
		}

		if (!path.IsFolder)
		{
			await Task.Run(() =>
			{
				try
				{
					var info = new ProcessStartInfo
					{
						FileName = path.GetPath(x => x.ToString()),
						UseShellExecute = true,
					};

					Process.Start(info);
				}
				catch (Exception)
				{
				}
			});
		}
		else
		{
			FileSystemTreeItem? item = null;

			foreach (var parent in path.EnumerateToRoot().Reverse())
			{
				item = new FileSystemTreeItem(parent.Value, true, item);
			}

			TreeItem = item;
			await UpdateFiles(false, "*");
		}
	}

	private static IEnumerable<FileModel> GetFileSystemEntriesRecursive(FileSystemTreeItem path, string search)
	{
		if (search is "*" or "*.*")
		{
			return path
				.EnumerateChildren((ref FileSystemEntry file) => !file.IsHidden)
				.Select(s => new FileModel(s));
		}

		return path
			.EnumerateChildren((ref FileSystemEntry entry) => entry.IsDirectory || FileSystemName.MatchesSimpleExpression(search, entry.FileName))
			.Where(w =>
			{
        if (!w.IsFolder)
        {
          return true;
        }

        using var buffer = new Buffer<char>(w.DynamicString.Length);

				w.DynamicString.CopyToSpan(buffer);

				return FileSystemName.MatchesSimpleExpression(search, buffer);
			})
			.Select(s => new FileModel(s));
	}

	private static IEnumerable<FileModel> GetFileSystemEntries(FileSystemTreeItem path)
	{
		return path
			.EnumerateChildren((ref FileSystemEntry file) => !file.IsHidden, 0)
			.Select(s => new FileModel(s));
	}

	private static int GetFileSystemEntriesCount(FileSystemTreeItem path, string search, CancellationToken token)
	{
		return path.GetChildrenCount();
	}
}