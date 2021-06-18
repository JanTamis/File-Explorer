using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileExplorerCore.Helpers;
using FileExplorerCore.Models;
using NetFabric.Hyperlinq;
using System;

namespace FileExplorerCore.DisplayViews
{
	public partial class FileGrid : UserControl
	{
		private int anchorIndex = 0;
		public event Action<string> PathChanged = delegate { };

		public ObservableRangeCollection<FileModel> Files
		{
			set
			{
				var grid = this.FindControl<ItemsRepeater>("fileList");

				grid.Items = value;
			}
			get
			{
				var grid = this.FindControl<ItemsRepeater>("fileList");

				return grid.Items as ObservableRangeCollection<FileModel>;
			}
		}

		public FileGrid() : base()
		{
			AvaloniaXamlLoader.Load(this);

			var grid = this.FindControl<ItemsRepeater>("fileList");

			grid.ElementPrepared += Grid_ElementPrepared;
			grid.ElementClearing += Grid_ElementClearing;

			grid.KeyDown += Grid_KeyDown;
		}

		private void Grid_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
		{
			if (e.Key is Key.A && e.KeyModifiers is KeyModifiers.Control)
			{
				foreach (var file in Files.AsValueEnumerable().Where(x => !x.IsSelected))
				{
					file.IsSelected = true;
				}
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

		private void Item_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
		{
			if (sender is ListBoxItem { DataContext: FileModel model } item)
			{
				//model.IsSelected = !model.IsSelected;

				//foreach (var file in Files.AsValueEnumerable().Where(x => x.IsSelected && x != model))
				//{
				//	file.IsSelected = false;
				//}

				var point = e.GetCurrentPoint(item);

				if (point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed)
				{
					var index = Files.IndexOf(model);

					UpdateSelection(
												index,
												true,
												e.KeyModifiers.HasAllFlags(KeyModifiers.Shift),
												e.KeyModifiers.HasAllFlags(KeyModifiers.Control),
												point.Properties.IsRightButtonPressed);
				}
			}
		}

		private void Grid_ElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
		{
			if (e.Element is ListBoxItem item)
			{
				item.DoubleTapped += Item_DoubleTapped;
				item.PointerPressed += Item_PointerPressed;
			}
		}

		private void Item_DoubleTapped(object? sender, RoutedEventArgs e)
		{
			if (sender is ListBoxItem { DataContext: FileModel model })
			{
				PathChanged(model.Path);
			}
		}

		/// <summary>
		/// Updates the selection for an item based on user interaction.
		/// </summary>
		/// <param name="index">The index of the item.</param>
		/// <param name="select">Whether the item should be selected or unselected.</param>
		/// <param name="rangeModifier">Whether the range modifier is enabled (i.e. shift key).</param>
		/// <param name="toggleModifier">Whether the toggle modifier is enabled (i.e. ctrl key).</param>
		/// <param name="rightButton">Whether the event is a right-click.</param>
		protected void UpdateSelection(
				int index,
				bool select = true,
				bool rangeModifier = false,
				bool toggleModifier = false,
				bool rightButton = false)
		{
			var files = Files;

			if (index < 0 || index >= files.Count)
			{
				return;
			}

			var mode = SelectionMode.Multiple;
			var multi = mode.HasAllFlags(SelectionMode.Multiple);
			var toggle = toggleModifier || mode.HasAllFlags(SelectionMode.Toggle);
			var range = multi && rangeModifier;

			if (!select)
			{
				files[index].IsSelected = false;
			}
			else if (range)
			{
				foreach (var file in Files.AsValueEnumerable().Where(x => x.IsSelected))
				{
					file.IsSelected = false;
				}

				if (index > anchorIndex)
				{
					for (int i = anchorIndex; i <= index; i++)
					{
						files[i].IsSelected = true;
					}
				}
				else
				{
					for (int i = index; i <= anchorIndex; i++)
					{
						files[i].IsSelected = true;
					}
				}
			}
			else if (multi && toggle)
			{
				if (files[index].IsSelected)
				{
					files[index].IsSelected = false;
				}
				else
				{
					files[index].IsSelected = true;
				}
			}
			else
			{
				foreach (var file in Files.AsValueEnumerable().Where(x => x.IsSelected))
				{
					file.IsSelected = false;
				}

				files[index].IsSelected = true;
			}

			if (!range)
			{
				anchorIndex = index;
			}
		}
	}
}
