using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using FileExplorer.Interfaces;
using FileExplorer.Models;

namespace FileExplorer.DisplayViews;

public partial class FileGrid : UserControl, ISelectableControl, IFileViewer
{
	private int anchorIndex = 0;
	new event PropertyChangedEventHandler? PropertyChanged = delegate { };

	public event Action<string> PathChanged = delegate { };
	public event Action SelectionChanged = delegate { };

	private ObservableRangeCollection<IFileItem> _items;

	public Action SelectAll { get; }
	public Action SelectNone { get; }
	public Action SelectInvert { get; }

	public Task<int> ItemCount { get; set; }

	public ObservableRangeCollection<IFileItem> Items
	{
		get => _items;
		set => OnPropertyChanged(ref _items, value);
	}

	public FileGrid()
	{
		AvaloniaXamlLoader.Load(this);

		DataContext = this;

		var grid = this.FindControl<ItemsRepeater>("fileList");

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
		if (e.Element is ListBoxItem { DataContext: FileModel model } item)
		{
			item.DoubleTapped -= Item_DoubleTapped;
			item.PointerPressed -= Item_PointerPressed;

			model.IsVisible = false;
		}
	}

	private async void Item_PointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (sender is ListBoxItem { DataContext: FileModel model } item)
		{
			var point = e.GetCurrentPoint(item);

			if (point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed)
			{
				var index = IndexOf(Items, model);

				anchorIndex = await IFileViewer.UpdateSelection(
					this,
					anchorIndex,
					index,
					true,
					e.KeyModifiers.HasAllFlags(KeyModifiers.Shift),
					e.KeyModifiers.HasAllFlags(KeyModifiers.Control));
			}
		}

		static int IndexOf<T>(IEnumerable<T> items, T item)
		{
			var index = 0;

			foreach (var data in items)
			{
				if (data?.Equals(item) == true)
				{
					return index;
				}

				index++;
			}

			return -1;
		}
	}

	private void Grid_ElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
	{
		if (e.Element is ListBoxItem { DataContext: FileModel model } item)
		{
			item.DoubleTapped += Item_DoubleTapped;
			item.PointerPressed += Item_PointerPressed;

			model.IsVisible = true;
		}
	}

	private void Item_DoubleTapped(object? sender, RoutedEventArgs e)
	{
		if (sender is ListBoxItem { DataContext: FileModel model })
		{
			PathChanged(model.TreeItem.GetPath(path => path.ToString()));
		}
	}

	public void OnPropertyChanged<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		field = value;
		PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
	}
}