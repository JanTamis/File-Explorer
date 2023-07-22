using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FileExplorer.Controls;
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

[ObservableObject]
public partial class TabItemViewModel
{
	private readonly Stack<IFileItem?> _undoStack = new();
	private readonly Stack<IFileItem?> _redoStack = new();

	public CancellationTokenSource? TokenSource;

	private bool _isUserEntered = true;

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
	private ObservableRangeCollection<IFileItem> _files = new();

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
				ViewTypes.Grid => new FileGrid { Provider = Provider, Items = Files, },
				ViewTypes.List => new FileTreeGrid { Provider = Provider, Items = Files, },
				_ => new Quickstart(),
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
	[NotifyPropertyChangedFor(nameof(PopupWidth))]
	[NotifyPropertyChangedFor(nameof(PopupHeight))]
	private int _SelectionCount;

	[ObservableProperty]
	private Control _popupControl;

	public double PopupWidth
	{
		get
		{
			var typeface = new Typeface(FontFamily.Parse("Roboto"));

			var defaultProperties = new GenericTextRunProperties(
				typeface,
				16,
				TextDecorations.Baseline);

			var paragraphProperties = new GenericTextParagraphProperties(FlowDirection.LeftToRight, TextAlignment.Left, true, false,
				defaultProperties, TextWrapping.NoWrap, Double.NaN, 0, 0);
			
			var textSource = new SimpleTextSource(FileCountText, defaultProperties);

			using var layout = new TextLayout(textSource, paragraphProperties);

			return Math.Max(layout.Width + 16, HasSelection ? PopupControl.Width : 0);
		}
	}

	public double PopupHeight
	{
		get
		{
			var typeface = new Typeface(FontFamily.Parse("Roboto"));

			var defaultProperties = new GenericTextRunProperties(
				typeface,
				16,
				TextDecorations.Baseline);

			var paragraphProperties = new GenericTextParagraphProperties(FlowDirection.LeftToRight, TextAlignment.Left, true, false,
				defaultProperties, TextWrapping.NoWrap, Double.NaN, 0, 0);

			var textSource = new SimpleTextSource(FileCountText, defaultProperties);

			using var layout = new TextLayout(textSource, paragraphProperties);

			return layout.Height + 16 + (HasSelection ? PopupControl.Height : 0);
		}
	}

	public bool HasSelection => SelectionCount > 0;

	public string FileCountText
	{
		get
		{
			var prefix = FileCount == 1
				? ResourceDefault.Item
				: ResourceDefault.Items;

			var number = (double)FileCount;
			var exponent = 0D;
			var selectionCount = SelectionCount;

			// if (!PointerOverAmount)
			// {
			// 	exponent = Math.Max(0, Math.Floor(Math.Log10(number) / 3));
			// 	number = FileCount * Math.Pow(1000, -exponent);
			// }

			if (exponent is 0)
			{
				
				return selectionCount > 0
					? String.Format(ResourceDefault.FileSelectionFormat, number.ToString("N0"), selectionCount.ToString("N0"))
					: $"{number:N0} {prefix}";
			}

			var symbol = exponent switch
			{
				1 => 'k',
				2 => 'M',
				3 => 'B',
				4 => 'T',
				5 => 'P',
				6 => 'E',
				_ => default,
			};

			return selectionCount > 0
				? $"{number:N2}{symbol} {prefix}\n{selectionCount:N0} {ResourceDefault.Selected}"
				: $"{number:N2}{symbol} {prefix}";
		}
	}

	public Task<IEnumerable<IPathSegment>> Folders => Provider.GetPathAsync(CurrentFolder).AsTask();

	public IEnumerable<Control> MenuItems => Provider.GetMenuItems(CurrentFolder)
		.Select<MenuItemModel, Control>(s =>
		{
			switch (s.Type)
			{
				case MenuItemType.Button:
					var button = new Button
					{
						Content = new MaterialIcon
						{
							Width = 25,
							Height = 25,
							[!TemplatedControl.ForegroundProperty] = new DynamicResourceExtension("PrimaryHueMidForegroundBrush"),
							Kind = Enum.Parse<MaterialIconKind>(s.Icon),
						},
					};
					
					button.Classes.Add("Icon");

					button.SetValue(Button.ForegroundProperty, Application.Current.FindResource("MaterialDesignBody"));

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
					var border = new Border
					{
						Width = 4,
						Margin = new Thickness(4, 0),
					};
					
					border.Classes.Add("Separator");
					return border;
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

	public bool PopupVisible => _popupContent is not null;

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
		if (TokenSource is { IsCancellationRequested: false })
		{
			IsLoading = false;
			TokenSource.Cancel();
		}
	}

	public async Task UpdateFiles(bool recursive, string search)
	{
		if (_updateNotificator is not null)
		{
			// _updateNotificator.Changed -= UpdateFolder;
			_updateNotificator.Dispose();
		}
		
		TokenSource?.Cancel();
		TokenSource = new CancellationTokenSource();

		Files.Clear();

		SelectionCount = 0;
		IsLoading = true;

		// _updateNotificator = Provider.GetNotificator(CurrentFolder, search, recursive);

		// if (_updateNotificator is not null)
		// {
		// 	_updateNotificator.Changed += UpdateFolder;
		// }

		await await Runner.Run<Task>(async () =>
		{
			var items = Provider.GetItemsAsync(CurrentFolder!, search, recursive, TokenSource.Token);

			if (recursive)
			{
				if (CurrentSortMode is SortEnum.None)
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
					Comparer<IFileItem> comparer;

					if (SortAcending)
					{
						comparer = CurrentSortMode switch
						{
							SortEnum.Edited => Comparer<IFileItem>.Create((x, y) => x.EditedOn.CompareTo(y.EditedOn)),
							SortEnum.Name => Comparer<IFileItem>.Create((x, y) => String.Compare(x.Name, y.Name, StringComparison.CurrentCulture)),
							SortEnum.Extension => Comparer<IFileItem>.Create((x, y) => String.Compare(x.Extension, y.Extension, StringComparison.CurrentCulture)),
							SortEnum.Size => Comparer<IFileItem>.Create((x, y) => x.Size.CompareTo(y.Size)),
						};
					}
					else
					{
						comparer = CurrentSortMode switch
						{
							SortEnum.Edited => Comparer<IFileItem>.Create((x, y) => y.EditedOn.CompareTo(x.EditedOn)),
							SortEnum.Name => Comparer<IFileItem>.Create((x, y) => String.Compare(y.Name, x.Name, StringComparison.CurrentCulture)),
							SortEnum.Extension => Comparer<IFileItem>.Create((x, y) => String.Compare(y.Extension, x.Extension, StringComparison.CurrentCulture)),
							SortEnum.Size => Comparer<IFileItem>.Create((x, y) => y.Size.CompareTo(x.Size)),
						};
					}

					await Files.AddRangeAsync(items, comparer, TokenSource.Token);
				}
			}
			else
			{
				if (CurrentSortMode is SortEnum.None)
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
				else
				{
					Comparer<IFileItem> comparer;

					if (SortAcending)
					{
						comparer = CurrentSortMode switch
						{
							SortEnum.Edited => Comparer<IFileItem>.Create((x, y) => x.EditedOn.CompareTo(y.EditedOn)),
							SortEnum.Name => Comparer<IFileItem>.Create((x, y) => String.Compare(x.Name, y.Name, StringComparison.CurrentCulture)),
							SortEnum.Extension => Comparer<IFileItem>.Create((x, y) => String.Compare(x.Extension, y.Extension, StringComparison.CurrentCulture)),
							SortEnum.Size => Comparer<IFileItem>.Create((x, y) => x.Size.CompareTo(y.Size)),
						};
					}
					else
					{
						comparer = CurrentSortMode switch
						{
							SortEnum.Edited => Comparer<IFileItem>.Create((x, y) => y.EditedOn.CompareTo(x.EditedOn)),
							SortEnum.Name => Comparer<IFileItem>.Create((x, y) => String.Compare(y.Name, x.Name, StringComparison.CurrentCulture)),
							SortEnum.Extension => Comparer<IFileItem>.Create((x, y) => String.Compare(y.Extension, x.Extension, StringComparison.CurrentCulture)),
							SortEnum.Size => Comparer<IFileItem>.Create((x, y) => y.Size.CompareTo(x.Size)),
						};
					}

					await Files.AddRangeAsync(items, comparer, TokenSource.Token);
				}
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

	partial void OnPopupContentChanged(IPopup? value)
	{
		if (value is not null)
		{
			value.OnClose += () =>
			{
				_popupContent = null;

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
		UpdateFiles(false, "*");
	}

	partial void OnSortAcendingChanged(bool value)
	{
		UpdateFiles(false, "*");
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
}