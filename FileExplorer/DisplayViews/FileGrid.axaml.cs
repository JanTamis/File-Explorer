using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.Interfaces;

namespace FileExplorer.DisplayViews;

public sealed partial class FileGrid : UserControl, ISelectableControl, IFileViewer
{
	private int _anchorIndex = 0;

	private bool _isShiftPressed;
	private bool _isCtrlPressed;

	private new event PropertyChangedEventHandler? PropertyChanged = delegate { };

	public event Action<IFileItem> PathChanged = delegate { };
	public event Action<int> SelectionChanged = delegate { };

	private ObservableRangeCollection<IFileItem>? _items;

	public void SelectAll()
	{
		if (Items is not null)
		{
			foreach (var item in Items)
			{
				item.IsSelected = true;
			}
		}
	}

	public void SelectNone()
	{
		if (Items is not null)
		{
			foreach (var item in Items)
			{
				item.IsSelected = false;
			}
		}
	}

	public void SelectInvert()
	{
		if (Items is not null)
		{
			foreach (var item in Items)
			{
				item.IsSelected ^= true;
			}
		}
	}

	public ObservableRangeCollection<IFileItem>? Items
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

		DoubleTappedEvent.Raised.Subscribe(e =>
		{
			if (e.Item1 is ToggleButton { DataContext: IFileItem model, })
			{
				PathChanged(model);
			}
		});

		KeyDownEvent.Raised.Subscribe(e =>
		{
			if (e.Item2 is KeyEventArgs args)
			{
				_isShiftPressed = args.KeyModifiers.HasFlag(KeyModifiers.Shift);
				_isCtrlPressed = args.KeyModifiers.HasFlag(KeyModifiers.Control);
			}
		});

		KeyUpEvent.Raised.Subscribe(e =>
		{
			if (e.Item2 is KeyEventArgs args)
			{
				_isShiftPressed = args.KeyModifiers.HasFlag(KeyModifiers.Shift);
				_isCtrlPressed = args.KeyModifiers.HasFlag(KeyModifiers.Control);
			}
		});

		fileList.ElementPrepared += Grid_ElementPrepared;
		fileList.ElementClearing += Grid_ElementClearing;

		fileList.KeyDown += Grid_KeyDown;
	}
	
	public FileGrid(IItemProvider provider, ObservableRangeCollection<IFileItem>? items) : this()
	{
		Provider = provider;
		Items = items;
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
		if (e.Element is ToggleButton item)
		{
			item.DoubleTapped -= Item_DoubleTapped;
			item.PointerPressed -= Item_PointerPressed;
		}
	}

	private void Item_PointerPressed(object? sender, RoutedEventArgs e)
	{
		if (sender is ToggleButton { DataContext: IFileItem model, } item)
		{
			var index = Items.IndexOf(model);

			_anchorIndex = IFileViewer.UpdateSelection(
				this,
				_anchorIndex,
				index,
				out var amount,
				true,
				_isShiftPressed,
				_isCtrlPressed);
			
			SelectionChanged?.Invoke(amount);
		}
	}

	private void Grid_ElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
	{
		if (e.Element is ToggleButton { DataContext: IFileItem model, } item)
		{
			item.DoubleTapped += Item_DoubleTapped;
			item.Click += Item_PointerPressed;

			model.IsVisible = true;
		}
	}

	private void Item_DoubleTapped(object? sender, RoutedEventArgs e)
	{
		if (sender is ToggleButton { DataContext: IFileItem model, })
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