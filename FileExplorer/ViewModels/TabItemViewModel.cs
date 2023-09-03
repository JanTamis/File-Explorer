using System.Globalization;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FileExplorer.Core.Extensions;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.Core.Models;
using FileExplorer.DisplayViews;
using FileExplorer.Interfaces;
using FileExplorer.Models;
using FileExplorer.Providers;
using FileExplorer.Resources;
using Material.Icons;
using Material.Icons.Avalonia;

namespace FileExplorer.ViewModels;

public partial class TabItemViewModel : ObservableObject
{
	private static readonly CompositeFormat FileSelectedFormat = CompositeFormat.Parse(ResourceDefault.FileSelectionFormat);

	private readonly Stack<IFileItem?> _undoStack = new();
	private readonly Stack<IFileItem?> _redoStack = new();

	public CancellationTokenSource? TokenSource;

	private bool _isUserEntered = true;
	private bool _isRecursive;

	private IFolderUpdateNotificator? _updateNotificator;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(DisplayControl))]
	[NotifyPropertyChangedFor(nameof(ShowToolbar))]
	private ViewTypes? _currentViewMode;

	[ObservableProperty]
	private SortEnum _currentSortMode = SortEnum.None;

	[ObservableProperty]
	private bool _sortAcending = true;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(FileCountText))]
	private bool _pointerOverAmount;

	[ObservableProperty]
	private string _search = String.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(FolderName))]
	[NotifyPropertyChangedFor(nameof(Folders))]
	[NotifyPropertyChangedFor(nameof(MenuItems))]
	private IFileItem? _currentFolder;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SearchFailed))]
	private ObservableRangeCollection<IFileItem>? _files = new();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SearchFailed))]
	private bool _isLoading;

	[ObservableProperty]
	private bool _isSelected;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SearchFailed))]
	[NotifyPropertyChangedFor(nameof(FileCountText))]
	private int _fileCount;

	public IFileViewer DisplayControl
	{
		get
		{
			IFileViewer control = CurrentViewMode switch
			{
				ViewTypes.Grid => new FileGrid(Provider, Files),
				ViewTypes.List => new FileTreeGrid(Provider, Files),
				_              => new Quickstart()
			};

			control.PathChanged += async path => await SetPath(path);
			control.SelectionChanged += count => SelectionCount = count;

			return control;
		}
	}

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PopupVisible))]
	private IPopup? _popupContent;

	[ObservableProperty]
	private SortEnum _sort = SortEnum.None;

	[ObservableProperty]
	private IItemProvider _provider = new FileSystemProvider();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(FileCountText))]
	[NotifyPropertyChangedFor(nameof(HasSelection))]
	private int _selectionCount;

	[ObservableProperty]
	private Control? _popupControl;

	public bool HasSelection => SelectionCount > 0;

	public string FileCountText
	{
		get
		{
			var prefix = FileCount == 1
				? ResourceDefault.Item
				: ResourceDefault.Items;

			return HasSelection
				? String.Format(CultureInfo.CurrentCulture, FileSelectedFormat, FileCount, SelectionCount)
				: $"{FileCount:N0} {prefix}";
		}
	}

	public Task<IEnumerable<IPathSegment>> Folders => Provider.GetPathAsync(CurrentFolder).AsTask();

	public IEnumerable<Control> MenuItems => Provider
		.GetMenuItems(CurrentFolder)
		.Select<MenuItemModel, Control>(s =>
		{
			switch (s.Type)
			{
				case MenuItemType.Button:
					var button = new Button
					{
						[!TemplatedControl.ForegroundProperty] = new DynamicResourceExtension("MaterialDesignBody"),
						Classes = { "Icon", },
						Content = new MaterialIcon
						{
							Width = 25,
							Height = 25,
							[!TemplatedControl.ForegroundProperty] = new DynamicResourceExtension("PrimaryHueMidForegroundBrush"),
							Kind = Enum.Parse<MaterialIconKind>(s.Icon)
						}
					};

					if (s.Action is not null)
					{
						button.Click += delegate
						{
							var model = new MenuItemActionModel
							{
								CurrentFolder = CurrentFolder,
								Files = Files,
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
						Width = 4,
						Margin = new Thickness(4, 0),
						Classes = { "Separator", }
					};
				case MenuItemType.Dropdown:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			throw new ArgumentOutOfRangeException();
		});

	public bool SearchFailed => !IsLoading && FileCount == 0 && CurrentFolder is not null;

	public string? FolderName => CurrentFolder?.Name;

	public bool IsSearching { get; set; }

	public bool PopupVisible => PopupContent is not null;

	public bool ShowToolbar => CurrentViewMode.HasValue;

	public TabItemViewModel()
	{
		Files.CountChanged += count => FileCount = count;
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
		if (TokenSource is { IsCancellationRequested: false, })
		{
			IsLoading = false;
			TokenSource.Cancel();
		}
	}

	public async Task UpdateFiles(bool recursive, string search)
	{
		_isRecursive = recursive;

		if (_updateNotificator is not null)
		{
			_updateNotificator.Changed -= UpdateFolder;
			_updateNotificator.Dispose();
		}

		await (TokenSource?.CancelAsync() ?? Task.CompletedTask);
		TokenSource = new CancellationTokenSource();

		Files.Clear();

		SelectionCount = 0;
		IsLoading = true;

		_updateNotificator = Provider.GetNotificator(CurrentFolder, search, recursive);

		if (_updateNotificator is not null)
		{
			_updateNotificator.Changed += UpdateFolder;
		}

		await await Runner.Run<Task>(async () =>
		{
			var items = Provider.GetItemsAsync(CurrentFolder!, search, recursive, TokenSource.Token);

			if (recursive && CurrentSortMode is SortEnum.None)
			{
				if (Provider is FileSystemProvider)
				{
					await Files.AddRangeAsync(Provider, CurrentFolder, search, TokenSource.Token);
				}
				else
				{
					await Files.AddRangeAsync(items, TokenSource.Token);
				}
			}
			else
			{
				await Files.AddRangeAsync(items, GetComparer(), TokenSource.Token);
			}
		});

		IsLoading = false;
	}

	private void UpdateFolder(IFolderUpdateNotificator notificator, ChangeType changeType, string oldPath, string? newPath)
	{
		if (IsLoading || Files is null)
		{
			return;
		}

		var fileFromOldPathEnumerable = Files
			.Where(w => w.GetPath((currentItem, item) => currentItem.SequenceEqual(item), oldPath));

		switch (changeType)
		{
			case ChangeType.Changed:

				foreach (var file in fileFromOldPathEnumerable)
				{
					Dispatcher.UIThread.Invoke(file.UpdateData);
					break;
				}

				break;

			case ChangeType.Created:
				if (_isRecursive)
				{
				}
				else if (CurrentFolder is FileModel file)
				{
					var comparer = GetComparer();
					var model = new FileModel(new FileSystemTreeItem(Path.GetFileName(oldPath.AsSpan()), new DirectoryInfo(oldPath).Exists, file.TreeItem));
					var index = Files.BinarySearch(model, comparer);

					if (index < 0)
					{
						Files.Insert(~index, model);
					}
				}

				break;

			case ChangeType.Deleted:
				for (var i = Files.Count - 1; i >= 0; i--)
				{
					if (Files[i].GetPath((currentItem, item) => currentItem.SequenceEqual(item), oldPath))
					{
						Files.RemoveAt(i);
					}
				}

				break;

			case ChangeType.Renamed:
				foreach (var file in fileFromOldPathEnumerable)
				{
					file.Name = Path.GetFileNameWithoutExtension(newPath)!;
					file.Extension = Path.GetExtension(newPath)!;
					Dispatcher.UIThread.Invoke(file.UpdateData);
					break;
				}

				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(changeType), changeType, null);
		}
	}

	public async ValueTask SetPath(IFileItem? path)
	{
		CurrentFolder = path;

		await UpdateFiles(false, "*");
	}

	async partial void OnFilesChanged(ObservableRangeCollection<IFileItem>? value)
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

	partial void OnPopupContentChanged(IPopup? value)
	{
		if (value is not null)
		{
			value.OnClose += () =>
			{
				PopupContent = null;

				OnPropertyChanged(nameof(PopupVisible));
				OnPropertyChanged(nameof(PopupContent.HasShadow));
				OnPropertyChanged(nameof(PopupContent.HasToBeCanceled));
			};
		}
	}

	partial void OnIsLoadingChanged(bool value)
	{
		GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false, true);
	}

	partial void OnCurrentSortModeChanged(SortEnum value)
	{
		if (IsLoading)
		{
			UpdateFiles(false, "*");
		}
		else
		{
			Files.Sort(GetSortComparison(value, SortAcending));
		}
	}

	partial void OnSortAcendingChanged(bool value)
	{
		if (IsLoading)
		{
			UpdateFiles(false, "*");
		}
		else
		{
			Files.Sort(GetSortComparison(Sort, value));
		}
	}

	partial void OnCurrentFolderChanged(IFileItem? value)
	{
		if (value is null && DisplayControl is not Quickstart)
		{
			CurrentViewMode = null;
		}
		else if (!CurrentViewMode.HasValue)
		{
			CurrentViewMode = ViewTypes.List;
		}

		if (_isUserEntered && (!_undoStack.TryPeek(out var tempPath) || tempPath != CurrentFolder))
		{
			_undoStack.Push(CurrentFolder);
			_redoStack.Clear();
		}

		IsSearching = false;
	}

	private Comparer<IFileItem> GetComparer()
	{
		return Comparer<IFileItem>.Create(GetSortComparison(CurrentSortMode, SortAcending));
	}

	private static Comparison<IFileItem> GetSortComparison(SortEnum sortMode, bool acending)
	{
		if (acending)
		{
			return sortMode switch
			{
				SortEnum.Edited    => static (x, y) => x.EditedOn.CompareTo(y.EditedOn),
				SortEnum.Size      => static (x, y) => x.Size.CompareTo(y.Size),
				SortEnum.Name      => static (x, y) => String.Compare(x.Name, y.Name),
				SortEnum.Extension => static (x, y) => String.Compare(x.Extension, y.Extension),
				_ => static (x, y) =>
				{
					var result = y.IsFolder.CompareTo(x.IsFolder);

					if (result is 0)
					{
						result = String.Compare(x.Name, y.Name);
					}

					return result;
				}
			};
		}

		return sortMode switch
		{
			SortEnum.Edited    => static (x, y) => y.EditedOn.CompareTo(x.EditedOn),
			SortEnum.Size      => static (x, y) => y.Size.CompareTo(x.Size),
			SortEnum.Name      => static (x, y) => String.Compare(y.Name, x.Name),
			SortEnum.Extension => static (x, y) => String.Compare(y.Extension, x.Extension),
			_ => static (x, y) =>
			{
				var result = x.IsFolder.CompareTo(y.IsFolder);

				if (result is 0)
				{
					result = String.Compare(y.Name, x.Name);
				}

				return result;
			}
		};
	}
}