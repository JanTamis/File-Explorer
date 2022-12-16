using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FileExplorer.Core.Helpers;
using FileExplorer.DisplayViews;
using FileExplorer.Interfaces;
using FileExplorer.Providers;
using FileExplorer.Models;
using FileExplorer.Core.Interfaces;
using Avalonia.Controls;
using Avalonia.Svg.Skia;
using CommunityToolkit.HighPerformance.Buffers;
using FileExplorer.Core.Models;
using Image = Avalonia.Controls.Image;
using System.Collections.Concurrent;
using Microsoft.Graph;
using ChangeType = FileExplorer.Core.Models.ChangeType;

namespace FileExplorer.ViewModels;

[INotifyPropertyChanged]
public sealed partial class TabItemViewModel
{
	private readonly Stack<IFileItem?> _undoStack = new();
	private readonly Stack<IFileItem?> _redoStack = new();

	public CancellationTokenSource? TokenSource;

	private bool _isUserEntered = true;

	private IFolderUpdateNotificator? _updateNotificator;

	[ObservableProperty]
	private string _search = String.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(FolderName))]
	[NotifyPropertyChangedFor(nameof(Folders))]
	[NotifyPropertyChangedFor(nameof(MenuItems))]
	private IFileItem? _currentFolder;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SearchFailed))]
	private ObservableRangeCollection<IFileItem> _files = new();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SearchFailed))]
	private bool _isLoading;

	[ObservableProperty]
	private bool _isSelected;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SearchFailed))]
	private int _fileCount;

	[ObservableProperty]
	private ViewTypes _currentViewMode = ViewTypes.Tree;

	[ObservableProperty]
	private IFileViewer _displayControl;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PopupVisible))]
	private IPopup? _popupContent;

	[ObservableProperty]
	private SortEnum _sort = SortEnum.None;

	[ObservableProperty]
	private IItemProvider _provider = new FileSystemProvider();

	public int SelectionCount => Files.Count(c => c.IsSelected);

	public Task<IEnumerable<IPathSegment>> Folders => Provider.GetPathAsync(CurrentFolder).AsTask();

	public IEnumerable<IControl> MenuItems => Provider.GetMenuItems(CurrentFolder)
		.Select<MenuItemModel, IControl>(s =>
		{
			switch (s.Type)
			{
				case MenuItemType.Button:
					var source = SvgSource.Load<SvgSource>($"avares://FileExplorer/Assets/UIIcons/{s.Icon}.svg", null);

					var button = new Button
					{
						Classes = new Classes("Flat"),
						Content = new Image
						{
							Source = new SvgImage
							{
								Source = source,
							},
							Width = 30,
							Height = 30,
						},
						Width = 40,
						Height = 40,
					};

					if (s.Action is not null)
					{
						button.Click += delegate
						{
							var model = new MenuItemActionModel
							{
								Files = Files,
								CurrentFolder = CurrentFolder,
							};

							s.Action(model);

							if (model.Popup is not null)
							{
								PopupContent = model.Popup;
							}
						};
					}

					return button;
				case MenuItemType.Separator:
					return new Border
					{
						Width = 2,
						Margin = new Thickness(4, 0),
						Classes = new Classes("Separator"),
					};
				case MenuItemType.Dropdown:
					break;
			}

			throw new ArgumentOutOfRangeException();
		});

	public bool SearchFailed => !IsLoading && FileCount == 0;

	public string? FolderName => CurrentFolder?.Name;

	public bool IsSearching { get; set; }

	public bool PopupVisible => _popupContent is not null;

	public TabItemViewModel()
	{
		Files.CountChanged += count => FileCount = count;

		DisplayControl = new FileTreeGrid
		{
			Provider = Provider,
			Items = Files,
		};
	}

	public IFileItem? Undo()
	{
		if (_undoStack.TryPop(out var path))
		{
			_isUserEntered = false;

			_redoStack.Push(CurrentFolder);
		}

		return path;
	}

	public IFileItem? Redo()
	{
		if (_redoStack.TryPop(out var path))
		{
			_isUserEntered = false;

			_undoStack.Push(CurrentFolder);
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

		if (_updateNotificator is not null)
		{
			_updateNotificator.Changed -= UpdateFolder;
			_updateNotificator.Dispose();
		}
		
		_updateNotificator = Provider.GetNotificator(CurrentFolder, search, recursive);

		if (_updateNotificator is not null)
		{
			_updateNotificator.Changed += UpdateFolder;
		}

		var bag = new ConcurrentBag<IFileItem>();

		await Provider.EnumerateItemsAsync(CurrentFolder, bag.Add, CancellationToken.None).ConfigureAwait(false);

		await await Task.Run<Task>(async () =>
		{
			Files.Clear();

			OnPropertyChanged(nameof(FileCount));

			var items = Provider.GetItemsAsync(CurrentFolder!, search, recursive, TokenSource.Token);

			if (recursive)
			{
				if (Provider is FileSystemProvider)
				{
					var tempItems = Provider.GetItems(CurrentFolder, "*", false, TokenSource.Token)
						.Select(s =>
						{
							var result = Enumerable.Empty<IFileItem>();

							if (s.IsFolder)
							{
								result = Provider.GetItems(s, search, recursive, TokenSource.Token);
							}

							return result.Prepend(s);
						});

					await Files.AddRangeAsync(tempItems, TokenSource.Token);
				}
				else
				{
					await Files.AddRangeAsync(items, TokenSource.Token);
				}
			}
			else
			{
				await Files.AddRangeAsync(items, Comparer<IFileItem>.Create((x, y) =>
				{
					switch (x, y)
					{
						case (null, null): return 0;
						case (null, _): return -1;
						case (_, null): return 1;
					}

					var result = y.IsFolder.CompareTo(x.IsFolder);

					if (result is 0)
					{
						result = String.Compare(x.Name, y.Name, StringComparison.CurrentCulture);
					}

					return result;
				}), TokenSource.Token);
			}
		});

		IsLoading = false;
	}

	public async ValueTask SetPath(IFileItem? path)
	{
		if (path is { IsFolder: true })
		{
			CurrentFolder = path;

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

	partial void OnProviderChanging(IItemProvider value)
	{
		if (DisplayControl is FileTreeGrid treeGrid)
		{
			treeGrid.Provider = value;
		}
	}

	partial void OnProviderChanged(IItemProvider value)
	{
		switch (DisplayControl)
		{
			case FileTreeGrid treeGrid:
				treeGrid.Provider = value;
				break;
			case FileGrid grid:
				grid.Provider = value;
				break;
		}

		Files.Clear();
	}

	partial void OnDisplayControlChanged(IFileViewer value)
	{
		value.PathChanged += async path => await SetPath(path);
		value.SelectionChanged += () => OnPropertyChanged(nameof(SelectionCount));
	}

	partial void OnCurrentViewModeChanged(ViewTypes value)
	{
		DisplayControl = value switch
		{
			ViewTypes.Grid => new FileGrid
			{
				Provider = Provider,
				Items = Files,
			},
			ViewTypes.Tree => new FileTreeGrid
			{
				Provider = Provider,
				Items = Files,
			},
			_ => new FileTreeGrid
			{
				Items = Files,
			},
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

	partial void OnIsLoadingChanged(bool value)
	{
		GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false, true);
	}

	partial void OnCurrentFolderChanged(IFileItem? value)
	{
		if (value is null && DisplayControl is not Quickstart)
		{
			DisplayControl = new Quickstart();
		}

		if (_isUserEntered && (!_undoStack.TryPeek(out var tempPath) || tempPath != CurrentFolder))
		{
			_undoStack.Push(CurrentFolder);
			_redoStack.Clear();
		}

		IsSearching = false;
	}

	private void UpdateFolder(ChangeType type, string path, string? newPath)
	{
		switch (type)
		{
			case ChangeType.Changed:
				for (var i = Files.Count - 1; i >= 0; i--)
				{
					if (Files[i].GetPath((currentPath, toFind) => currentPath.Equals(toFind, StringComparison.CurrentCulture), path))
					{
						Files[i].UpdateData();
					}
				}
				break;
			case ChangeType.Created:
				if (_provider is FileSystemProvider)
				{
					for (var i = Files.Count - 1; i >= 0; i--)
					{
						if (Files[i].GetPath((currentPath, toFind) => currentPath.Equals(toFind, StringComparison.CurrentCulture), path))
						{
							return;
						}
					}

					_files.Add(new FileModel(FileSystemTreeItem.FromPath(path)));
				}
				break;
			case ChangeType.Deleted:
				for (var i = Files.Count - 1; i >= 0; i--)
				{
					if (Files[i].GetPath((currentPath, toFind) => currentPath.Equals(toFind, StringComparison.CurrentCulture), path))
					{
						Files.RemoveAt(i);
						break;
					}
				}
				break;
			case ChangeType.Renamed:
				for (var i = _files.Count - 1; i >= 0; i--)
				{
					if (Files[i].GetPath((currentPath, toFind) => currentPath.Equals(toFind, StringComparison.CurrentCulture), path))
					{
						Files[i].Name = System.IO.Path.GetFileName(newPath);
					}
				}
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(type), type, null);
		}
	}
}