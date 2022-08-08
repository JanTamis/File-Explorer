using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FileExplorer.DisplayViews;
using FileExplorer.Interfaces;
using FileExplorer.Providers;
using FileExplorer.Helpers;
using FileExplorer.Models;
using FileExplorer.Core.Interfaces;

namespace FileExplorer.ViewModels;

[INotifyPropertyChanged]
public partial class TabItemViewModel
{
	private readonly Stack<string?> _undoStack = new();
	private readonly Stack<string?> _redoStack = new();

	public CancellationTokenSource? TokenSource;

	private bool _isUserEntered = true;

	[ObservableProperty]
	private string _search = String.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(FolderName))]
	[NotifyPropertyChangedFor(nameof(Folders))]
	private string? _path;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SearchFailed))]
	private ObservableRangeCollection<IFileItem> _files = new();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SearchFailed))]
	private bool _isLoading;

	[ObservableProperty]
	private bool _isSelected;

	[ObservableProperty]
	private int _fileCount;

	[ObservableProperty]
	private ViewTypes _currentViewMode = ViewTypes.Tree;

	[ObservableProperty]
	private IFileViewer _displayControl = new Quickstart();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PopupVisible))]
	private IPopup? _popupContent;

	[ObservableProperty]
	private SortEnum _sort = SortEnum.None;

	[ObservableProperty]
	private IItemProvider _provider = new FileSystemProvider();

	public event Action? PathChanged;

	public int SelectionCount => Files.Count(file => file.IsSelected);

	public IEnumerable<IPathSegment> Folders => Provider.GetPath(Path);


	public bool SearchFailed => !IsLoading && !Files.Any() && DisplayControl is not Quickstart;

	public string? FolderName => System.IO.Path.GetDirectoryName(Path);

	public bool IsSearching { get; set; }

	public bool PopupVisible => _popupContent is not null;

	public TabItemViewModel()
	{
		Files.CountChanged += count => FileCount = count;
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

	public async Task UpdateFiles(bool recursive, string search)
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
						result = String.Compare(x.Name, y.Name, StringComparison.CurrentCulture);
					}

					return result;
				}), true, null, TokenSource.Token);
			}
		});

		IsLoading = false;
	}

	public async ValueTask SetPath(string? path)
	{
		if (!Directory.Exists(path))
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

	async partial void OnFilesChanged(ObservableRangeCollection<IFileItem> value)
	{
		if (Dispatcher.UIThread.CheckAccess())
		{
			DisplayControl.Items = value;
		}
		else
		{
			await Dispatcher.UIThread.InvokeAsync(() => DisplayControl.Items = value);
		}
	}

	partial void OnDisplayControlChanged(IFileViewer value)
	{
		value.PathChanged += async path => await SetPath(path);
		value.SelectionChanged += () => OnPropertyChanged(nameof(SelectionCount));

		value.Items = Files;
	}

	partial void OnCurrentViewModeChanged(ViewTypes value)
	{
		DisplayControl = value switch
		{
			ViewTypes.Grid => new FileGrid(),
			ViewTypes.List => new FileDataGrid(),
			ViewTypes.Tree => new FileTreeGrid(),
			_ => new FileDataGrid(),
		};
	}

	partial void OnPopupContentChanged(IPopup? value)
	{
		if (value is not null)
		{
			value.OnClose += () =>
			{
				_popupContent = null;
				OnPropertyChanged(nameof(PopupVisible));
			};
		}
	}

	partial void OnPathChanged(string? value)
	{
		PathChanged?.Invoke();

		if (value is null && DisplayControl is not Quickstart)
		{
			DisplayControl = new Quickstart();
		}
		else
		{
			OnCurrentViewModeChanged(CurrentViewMode);
		}

		if (_isUserEntered && (!_undoStack.TryPeek(out var tempPath) || tempPath != Path))
		{
			_undoStack.Push(Path);
			_redoStack.Clear();
		}

		IsSearching = false;
	}
}