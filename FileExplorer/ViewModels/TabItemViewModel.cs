using System.IO;
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
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using FileExplorer.Controls;
using FileExplorer.Core.Models;
using FileExplorer.Core.Extensions;
using FileExplorer.Resources;
using ChangeType = FileExplorer.Core.Models.ChangeType;
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

			return Math.Max(layout.Bounds.Width + 16, HasSelection ? PopupControl.Width : 0);
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

			return layout.Bounds.Height + 16 + (HasSelection ? PopupControl.Height : 0);
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

	public bool SearchFailed => !IsLoading && FileCount == 0;

	public string? FolderName => CurrentFolder?.Name;

	public bool IsSearching { get; set; }

	public bool PopupVisible => _popupContent is not null;

	public TabItemViewModel()
	{
		Files.CountChanged += count => FileCount = count;

		DisplayControl = new FileGrid
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
		if (_updateNotificator is not null)
		{
			_updateNotificator.Changed -= UpdateFolder;
			_updateNotificator.Dispose();
		}
		
		TokenSource?.Cancel();
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

			if (recursive)
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
		value.SelectionChanged += count =>
		{
			SelectionCount = count;

			// if (SelectionCount == 1)
			// {
			// 	var item = Files.FirstOrDefault(f => f.IsSelected);
			//
			// 	if (!item!.IsFolder && item.GetPath(path => Path.GetExtension(path) is ".jpg" or ".png" or ".bmp" or ".jpeg" or ".tiff"))
			// 	{
			// 		PopupControl = new Viewbox()
			// 		{
			// 			Width = 150,
			// 			Height = 150,
			// 			Margin = new Thickness(5),
			// 			Child = new Image
			// 			{
			// 				Width = 150,
			// 				Height = 150,
			// 				Source = new Bitmap(item.GetPath()),
			// 			},
			// 		};
			// 	}
			// 	else
			// 	{
			// 		PopupControl = new Image
			// 		{
			// 			Width = 100,
			// 			Height = 100,
			// 			Source = await Provider.GetThumbnailAsync(item, 100, default),
			// 		};
			// 	}
			// }
		};
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
				OnPropertyChanged(nameof(PopupContent.HasShadow));
				OnPropertyChanged(nameof(PopupContent.HasToBeCanceled));
			};
		}
	}

	partial void OnIsLoadingChanged(bool value)
	{
		// if (value)
		// {
		// 	GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
		// }

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

	private void UpdateFolder(IFolderUpdateNotificator updater, ChangeType type, string path, string? newPath)
	{
		if (_updateNotificator?.Equals(updater) != true)
		{
			return;
		}

		switch (type)
		{
			case ChangeType.Changed:
				for (var i = Files.Count - 1; i >= 0; i--)
				{
					i = Math.Min(i, Files.Count - 1);

					var file = Files[i];

					if (file?.GetPath((currentPath, toFind) => currentPath.Equals(toFind, StringComparison.CurrentCulture), path) == true)
					{
						file.UpdateData();
					}
				}

				break;
			case ChangeType.Created:
				if (_provider is FileSystemProvider)
				{
					for (var i = Files.Count - 1; i >= 0; i--)
					{
						i = Math.Min(i, Files.Count - 1);

						if (i < 0)
						{
							return;
						}

						var file = Files[i];

						if (file?.GetPath((currentPath, toFind) => currentPath.Equals(toFind, StringComparison.CurrentCulture), path) == true)
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
					i = Math.Min(i, Files.Count - 1);

					if (i < 0)
					{
						return;
					}

					var file = Files[i];

					if (file?.GetPath((currentPath, toFind) => currentPath.Equals(toFind, StringComparison.CurrentCulture), path) == true)
					{
						Files.Remove(file);
						break;
					}
				}

				break;
			case ChangeType.Renamed:
				for (var i = _files.Count - 1; i >= 0; i--)
				{
					i = Math.Min(i, Files.Count - 1);

					if (i < 0)
					{
						return;
					}

					var file = Files[i];

					if (file?.GetPath((currentPath, toFind) => currentPath.Equals(toFind, StringComparison.CurrentCulture), path) == true)
					{
						file.Name = System.IO.Path.GetFileName(newPath);
					}
				}

				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(type), type, null);
		}
	}
}