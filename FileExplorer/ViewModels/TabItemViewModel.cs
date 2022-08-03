using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FileExplorer.DisplayViews;
using FileExplorer.Interfaces;
using FileExplorer.Providers;
using FileExplorer.Helpers;
using FileExplorer.Models;
using FileExplorer.Core.Interfaces;

namespace FileExplorer.ViewModels;

public class TabItemViewModel : ViewModelBase
{
	private string _search = String.Empty;
	private string? _path;

	private ObservableRangeCollection<IFileItem> _files = new();

	private bool _isUserEntered = true;
	private bool _isLoading;
	private bool _isSelected;

	private ViewTypes _currentViewMode = ViewTypes.Tree;

	private readonly Stack<string?> _undoStack = new();
	private readonly Stack<string?> _redoStack = new();

	public CancellationTokenSource? TokenSource;
	private IFileViewer _displayControl = new Quickstart();

	private IPopup? _popupContent;

	private SortEnum _sort = SortEnum.None;
	private IItemProvider _provider = new FileSystemProvider();

	public event Action? PathChanged;

	public IItemProvider Provider
	{
		get => _provider;
		set => OnPropertyChanged(ref _provider, value);
	}

	public SortEnum Sort
	{
		get => _sort;
		set => OnPropertyChanged(ref _sort, value);
	}

	public int SelectionCount => Files.Count(file => file.IsSelected);

	public IEnumerable<IPathSegment> Folders => Provider.GetPath(Path);

	public bool IsSelected
	{
		get => _isSelected;
		set => OnPropertyChanged(ref _isSelected, value);
	}

	public int FileCount => Files.Count;

	public bool IsLoading
	{
		get => _isLoading;
		set
		{
			OnPropertyChanged(ref _isLoading, value);
			OnPropertyChanged(nameof(SearchFailed));
		}
	}

	public bool SearchFailed => !IsLoading && !Files.Any() && DisplayControl is not Quickstart;

	public ObservableRangeCollection<IFileItem> Files
	{
		get => _files;
		set
		{
			OnPropertyChanged(ref _files, value);
			OnPropertyChanged(nameof(SearchFailed));

			Dispatcher.UIThread.InvokeAsync(() => DisplayControl.Items = value);
		}
	}

	public string? Path
	{
		get => _path;
		set
		{
			OnPropertyChanged(ref _path, value);

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

			if (_isUserEntered && (!_undoStack.TryPeek(out var tempPath) || tempPath != Path))
			{
				_undoStack.Push(Path);
				_redoStack.Clear();
			}

			OnPropertyChanged(nameof(FolderName));
			OnPropertyChanged(nameof(Folders));

			IsSearching = false;
		}
	}

	public string? FolderName => System.IO.Path.GetDirectoryName(Path);

	public string Search
	{
		get => _search;
		set => OnPropertyChanged(ref _search, value);
	}

	public IFileViewer DisplayControl
	{
		get => _displayControl;
		set
		{
			OnPropertyChanged(ref _displayControl, value);

			value.PathChanged += async path => await SetPath(path);
			value.SelectionChanged += () => OnPropertyChanged(nameof(SelectionCount));

			value.Items = Files;
		}
	}

	public bool IsSearching { get; set; }

	public ViewTypes CurrentViewMode
	{
		get => _currentViewMode;
		set
		{
			OnPropertyChanged(ref _currentViewMode, value);

			DisplayControl = value switch
			{
				ViewTypes.Grid => new FileGrid(),
				ViewTypes.List => new FileDataGrid(),
				ViewTypes.Tree => new FileTreeGrid(),
				_ => new FileDataGrid(),
			};
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
				PopupContent.OnClose += () =>
				{
					_popupContent = null;
					OnPropertyChanged(nameof(PopupVisible));
				};
			}

			OnPropertyChanged(nameof(PopupVisible));
		}
	}

	public bool PopupVisible => _popupContent is not null;

	public TabItemViewModel()
	{
		CurrentViewMode = ViewTypes.Tree;
		Files.CountChanged += i => OnPropertyChanged(nameof(FileCount));
	}

	public string? Undo()
	{
		if (_undoStack.TryPop(out var path))
		{
			_isUserEntered = false;

			_redoStack.Push(Path);
		}

		return path;
	}

	public string? Redo()
	{
		if (_redoStack.TryPop(out var path))
		{
			_isUserEntered = false;

			_undoStack.Push(Path);
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
		TokenSource?.Cancel();
		TokenSource = new CancellationTokenSource();

		IsLoading = true;

		await Task.Run(async () =>
		{
			Files.Clear();

			OnPropertyChanged(nameof(FileCount));

			var items = Provider.GetItems(Path, search, recursive);

			if (recursive)
			{
				await Files.AddRange<IComparer<IFileItem>>(items, default, true, null, TokenSource.Token);
			}
			else
			{
				await Files.AddRange<IComparer<IFileItem>>(items, Comparer<IFileItem>.Create((x, y) =>
				{
					var result = y.IsFolder.CompareTo(x.IsFolder);

					if (result is 0)
					{
						result = x.Name.CompareTo(y.Name);
					}

					return result;
				}), true, null, TokenSource.Token);
			}
		});

		IsLoading = false;
	}

	public async ValueTask SetPath(string? path)
	{
		if (path is null)
		{
			Path = path;
			DisplayControl = new Quickstart();
		}
		else if (!Directory.Exists(path))
		{
			await Task.Run(() =>
			{
				try
				{
					var info = new ProcessStartInfo
					{
						FileName = path,
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
			Path = path;
			await UpdateFiles(false, "*");
		}
	}
}