using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using FileExplorer.Core.Interfaces;
using FileExplorer.Interfaces;
using FileExplorer.Models;

namespace FileExplorer.DisplayViews;

public partial class FileDataGrid : UserControl, ISelectableControl, IFileViewer
{
	private int anchorIndex = 0;
	public event Action<string> PathChanged = delegate { };
	public event Action SelectionChanged = delegate { };

	public Action SelectAll { get; }
	public Action SelectNone { get; }
	public Action SelectInvert { get; }

	public IEnumerable<IFileItem> Items
	{
		set
		{
			var grid = this.FindControl<ItemsRepeater>("fileList");

			grid.Items = value;
		}
		get
		{
			var grid = this.FindControl<ItemsRepeater>("fileList");

			return grid.Items as IEnumerable<IFileItem>;
		}
	}

	public Task<int> ItemCount { get; set; }

	public FileDataGrid() : base()
	{
		AvaloniaXamlLoader.Load(this);

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
				var index = Items.IndexOf(model);

				// anchorIndex = await IFileViewer.UpdateSelection(
				// 	this,
				// 	anchorIndex,
				// 	index,
				// 	true,
				// 	e.KeyModifiers.HasAllFlags(KeyModifiers.Shift),
				// 	e.KeyModifiers.HasAllFlags(KeyModifiers.Control));
			}
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
}