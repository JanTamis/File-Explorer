using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FileExplorer.Converters;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.Interfaces;
using Material.Ripple;

namespace FileExplorer.DisplayViews;

public partial class FileGrid : UserControl, ISelectableControl, IFileViewer
{
	private int anchorIndex = 0;
	new event PropertyChangedEventHandler? PropertyChanged = delegate { };

	public event Action<IFileItem> PathChanged = delegate { };
	public event Action SelectionChanged = delegate { };

	private ObservableRangeCollection<IFileItem> _items;
	private IItemProvider _provider;

	public void SelectAll()
	{
		BeginBatchUpdate();

		foreach (var item in Items)
		{
			item.IsSelected = true;
		}

		EndBatchUpdate();
	}

	public void SelectNone()
	{
		BeginBatchUpdate();

		foreach (var item in Items)
		{
			item.IsSelected = false;
		}

		EndBatchUpdate();
	}

	public void SelectInvert()
	{
		BeginBatchUpdate();

		foreach (var item in Items)
		{
			item.IsSelected ^= true;
		}

		EndBatchUpdate();
	}

	public ObservableRangeCollection<IFileItem> Items
	{
		get => _items;
		set
		{
			OnPropertyChanged(ref _items, value);

			var grid = this.FindControl<ItemsRepeater>("fileList");

			grid.Items = value;
		}
	}

	public IItemProvider Provider
	{
		get => _provider;
		set
		{
			_provider = value;

			var grid = this.FindControl<ItemsRepeater>("fileList");

			grid.ItemTemplate = new FuncDataTemplate<IFileItem>((x, _) =>
			{
				var box = new ListBoxItem
				{
					[!RippleEffect.RippleFillProperty] = new Binding("PrimaryHueMidForegroundBrush"),
					[!ListBoxItem.IsSelectedProperty] = new Binding("IsSelected"),
					Content = new StackPanel
					{
						Orientation = Orientation.Vertical,
						Margin = new Thickness(5),
						Children =
						{
							new Image
							{
								Width = 100,
								Height = 100,
								[!DataContextProperty] = new Binding
								{
									ConverterParameter = Provider,
									Converter = PathToImageConverter.Instance,
								},
								[!Image.SourceProperty] = new Binding("Result"),
							},
							new TextBlock
							{
								Margin = new Thickness(4, 2),
								TextTrimming = TextTrimming.CharacterEllipsis,
								TextAlignment = TextAlignment.Center,
								[!TextBlock.TextProperty] = new Binding("Name"),
							},
						},
					},
				};
				return box;
			});
		}
	}

	public FileGrid()
	{
		AvaloniaXamlLoader.Load(this);

		DataContext = this;

		var grid = this.FindControl<ItemsRepeater>("fileList");

		grid.ItemTemplate = new FuncDataTemplate<IFileItem>((x, _) =>
		{
			var box = new ListBoxItem
			{
				[!RippleEffect.RippleFillProperty] = new Binding("PrimaryHueMidForegroundBrush"),
				[!ListBoxItem.IsSelectedProperty] = new Binding("IsSelected"),
				Content = new StackPanel
				{
					Orientation = Orientation.Vertical,
					Margin = new Thickness(5),
					Children =
					{
						new Image
						{
							Width = 100,
							Height = 100,
							[!DataContextProperty] = new Binding
							{
								ConverterParameter = Provider,
								Converter = PathToImageConverter.Instance,
							},
							[!Image.SourceProperty] = new Binding("Result"),
						},
						new TextBlock
						{
							Margin = new Thickness(4, 2),
							TextTrimming = TextTrimming.CharacterEllipsis,
							TextAlignment = TextAlignment.Center,
							[!TextBlock.TextProperty] = new Binding("Name"),
						},
					},
				},
			};
			return box;
		});

		DataContextProperty.Changed.Subscribe(args =>
		{
			if (args.NewValue.GetValueOrDefault() is IFileItem newItem)
			{
				newItem.IsVisible = true;

				if (args.Sender is ListBoxItem item)
				{
					item.IsSelected = newItem.IsSelected;
				}
			}

			if (args.OldValue.GetValueOrDefault() is IFileItem oldItem)
			{
				oldItem.IsVisible = false;
			}
		});

		grid.ElementPrepared += Grid_ElementPrepared;
		grid.ElementClearing += Grid_ElementClearing;

		grid.KeyDown += Grid_KeyDown;
	}

	private void Grid_KeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key is Key.A && e.KeyModifiers is KeyModifiers.Control)
		{
			foreach (var file in Items.Where(x => !x.IsSelected))
			{
				file.IsSelected = true;
			}

			SelectionChanged?.Invoke();
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

				BeginBatchUpdate();

				anchorIndex = IFileViewer.UpdateSelection(
					this,
					anchorIndex,
					index,
					true,
					e.KeyModifiers.HasAllFlags(KeyModifiers.Shift),
					e.KeyModifiers.HasAllFlags(KeyModifiers.Control));

				EndBatchUpdate();
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
		PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
	}
}