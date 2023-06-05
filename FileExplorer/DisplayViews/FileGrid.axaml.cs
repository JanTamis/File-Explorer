using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.Interfaces;

namespace FileExplorer.DisplayViews;

public sealed partial class FileGrid : UserControl, ISelectableControl, IFileViewer
{
	private int anchorIndex = 0;
	new event PropertyChangedEventHandler? PropertyChanged = delegate { };

	public event Action<IFileItem> PathChanged = delegate { };
	public event Action<int> SelectionChanged = delegate { };

	private ObservableRangeCollection<IFileItem> _items;

	public void SelectAll()
	{
		foreach (var item in Items)
		{
			item.IsSelected = true;
		}
	}

	public void SelectNone()
	{
		foreach (var item in Items)
		{
			item.IsSelected = false;
		}
	}

	public void SelectInvert()
	{
		foreach (var item in Items)
		{
			item.IsSelected ^= true;
		}
	}

	public ObservableRangeCollection<IFileItem> Items
	{
		get => _items;
		set
		{
			OnPropertyChanged(ref _items, value);

			fileList.ItemsSource = value;
		}
	}

	public IItemProvider Provider { get; set; }

	public FileGrid()
	{
		InitializeComponent();

		DataContext = this;

		// var folder = new SvgImage
		// {
		// 	Source = SvgSource.Load<SvgSource>($"avares://FileExplorer/Assets/Icons/Folder.svg", null),
		// };
		// var file = new SvgImage
		// {
		// 	Source = SvgSource.Load<SvgSource>($"avares://FileExplorer/Assets/Icons/File.svg", null),
		// };
		//
		// grid.ItemTemplate = new FuncDataTemplate<IFileItem>((x, _) =>
		// {
		// 	var box = new ListBoxItem
		// 	{
		// 		[!RippleEffect.RippleFillProperty] = new Binding("PrimaryHueMidForegroundBrush"),
		// 		[!ListBoxItem.IsSelectedProperty] = new Binding("IsSelected"),
		// 		Content = new StackPanel
		// 		{
		// 			Orientation = Orientation.Vertical,
		// 			Margin = new Thickness(5),
		// 			Children =
		// 			{
		// 				new Panel
		// 				{
		// 					Margin = new Thickness(2.5, 0, 0, 0),
		// 					[!DataContextProperty] = new Binding
		// 					{
		// 						ConverterParameter = Provider,
		// 						Converter = PathToImageConverter.Instance,
		// 					},
		//
		// 					Children =
		// 					{
		// 						new Image
		// 						{
		// 							Width = 64,
		// 							Height = 64,
		// 							[!Image.IsVisibleProperty] = new Binding("IsSuccessfullyCompleted"),
		// 							[!Image.SourceProperty] = new Binding("Result"),
		// 						},
		//
		// 						new Image
		// 						{
		// 							Width = 64,
		// 							Height = 64,
		// 							[!Image.IsVisibleProperty] = new MultiBinding
		// 							{
		// 								Bindings =
		// 								{
		// 									new Binding("!IsSuccessfullyCompleted"),
		// 									new Binding("$parent[1].DataContext.IsFolder")
		// 								},
		// 								Converter = BoolConverters.And,
		// 							},
		// 							Source = folder,
		// 						},
		// 						new Image
		// 						{
		// 							Width = 64,
		// 							Height = 64,
		// 							[!Image.IsVisibleProperty] = new MultiBinding
		// 							{
		// 								Bindings =
		// 								{
		// 									new Binding("!IsSuccessfullyCompleted"),
		// 									new Binding("!$parent[1].DataContext.IsFolder")
		// 								},
		// 								Converter = BoolConverters.And,
		// 							},
		// 							Source = file,
		// 						},
		// 					},
		// 				},
		// 				new TextBlock
		// 				{
		// 					Margin = new Thickness(4, 2),
		// 					TextTrimming = TextTrimming.CharacterEllipsis,
		// 					TextAlignment = TextAlignment.Center,
		// 					[!TextBlock.TextProperty] = new Binding("Name"),
		// 				},
		// 			},
		// 		},
		// 	};
		// 	return box;
		// });

		DoubleTappedEvent.Raised.Subscribe(e =>
		{
			if (e.Item1 is ListBoxItem { DataContext: IFileItem model })
			{
				PathChanged(model);
			}
		});

		// fileList.double. += Grid_ElementPrepared;
		// fileList.ElementClearing += Grid_ElementClearing;
		//
		// fileList.KeyDown += Grid_KeyDown;
	}

	private void Grid_KeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key is Key.A && e.KeyModifiers is KeyModifiers.Control)
		{
			foreach (var file in Items.Where(x => !x.IsSelected))
			{
				file.IsSelected = true;
			}

			SelectionChanged?.Invoke(_items.Count);
		}
	}

	private void Grid_ElementClearing(object? sender, ItemsRepeaterElementClearingEventArgs e)
	{
		if (e.Element is ListBoxItem item)
		{
			item.DoubleTapped -= Item_DoubleTapped;
			item.PointerPressed -= Item_PointerPressed;
		}
	}

	private async void Item_PointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (sender is ListBoxItem { DataContext: IFileItem model } item)
		{
			var point = e.GetCurrentPoint(item);

			if (point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed)
			{
				var index = Items.IndexOf(model);
				
				anchorIndex = IFileViewer.UpdateSelection(
					this,
					anchorIndex,
					index,
					true,
					e.KeyModifiers.HasFlag(KeyModifiers.Shift),
					e.KeyModifiers.HasFlag(KeyModifiers.Control));
			}
		}
	}

	private void Grid_ElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
	{
		if (e.Element is ListBoxItem { DataContext: IFileItem model } item)
		{
			item.DoubleTapped += Item_DoubleTapped;
			item.PointerPressed += Item_PointerPressed;

			model.IsVisible = true;
		}
	}

	private void Item_DoubleTapped(object? sender, RoutedEventArgs e)
	{
		if (sender is ListBoxItem { DataContext: IFileItem model })
		{
			PathChanged(model);
		}
	}

	public void OnPropertyChanged<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}