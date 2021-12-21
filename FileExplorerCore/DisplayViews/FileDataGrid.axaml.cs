using System;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileExplorerCore.Helpers;
using FileExplorerCore.Models;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace FileExplorerCore.DisplayViews
{
	public partial class FileDataGrid : UserControl, INotifyPropertyChanged
	{
		private int anchorIndex;
		private double elementHeight;

		public event Action<string> PathChanged = delegate { };
		public new event PropertyChangedEventHandler PropertyChanged = delegate { };

		private ObservableRangeCollection<FileModel> _files;

		public ObservableRangeCollection<FileModel> Files
		{
			get => _files;
			set
			{
				OnPropertyChanged(ref _files, value);

				//_files.CollectionChanged += delegate
				//{
				//	OnPropertyChanged(nameof(View));

				//	var view = View;
				//	view.GroupDescriptions.Add(new DataGridPathGroupDescription("Extension"));

				//	var grid = this.FindControl<DataGrid>("fileList");

				//	grid.Items = View;
				//};
			}
		}

		public DataGridCollectionView View => new(Files);

		public FileDataGrid() : base()
		{
			AvaloniaXamlLoader.Load(this);

			//var grid = this.FindControl<ItemsRepeater>("fileList");

			//grid.ElementPrepared += Grid_ElementPrepared;
			//grid.ElementClearing += Grid_ElementClearing;

			//grid.KeyDown += Grid_KeyDown;

			var grid = this.FindControl<DataGrid>("fileList");

			grid.LoadingRow += Grid_LoadingRow;
			grid.UnloadingRow += Grid_UnloadingRow;

			grid.SelectionChanged += Grid_SelectionChanged;

			ListBoxItem.BoundsProperty.Changed.Subscribe(x =>
			{
				if (elementHeight < x.NewValue.Value.Height && x.Sender is ListBoxItem)
				{
					elementHeight = x.NewValue.Value.Height;
				}
			}, delegate { }, delegate { }, default);

			FileModel.SelectionChanged += (file) =>
			{
				if (grid.Items != null)
				{
					if (file.IsSelected)
					{
						grid.SelectedItems.Add(file);
					}
					else
					{
						grid.SelectedItems.Remove(file);
					}
				}
			};
		}

		private void Grid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			foreach (FileModel item in e.AddedItems)
			{
				item.IsSelected = true;
			}

			foreach (FileModel item in e.RemovedItems)
			{
				item.IsSelected = false;
			}
		}

		private void Grid_UnloadingRow(object? sender, DataGridRowEventArgs e)
		{
			e.Row.DoubleTapped -= Item_DoubleTapped;

			if (e.Row.DataContext is FileModel model)
			{
				model.IsVisible = false;
			}
		}

		private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
		{
			e.Row.DoubleTapped += Item_DoubleTapped;

			if (e.Row.DataContext is FileModel model)
			{
				model.IsVisible = true;
			}
		}

		private void Grid_KeyDown(object? sender, KeyEventArgs e)
		{
			if (sender is ItemsRepeater { Parent: ScrollViewer viewer } repeater)
			{
				if (e.Key is Key.A && e.KeyModifiers is KeyModifiers.Control)
				{
					foreach (var file in Files.Where(x => !x.IsSelected))
					{
						file.IsSelected = true;
					}
				}
				else if (e.Key is Key.Down)
				{
					anchorIndex = Math.Min(Files.Count - 1, anchorIndex + 1);

					UpdateSelection(anchorIndex);

					viewer.Offset = new Vector(0, anchorIndex * elementHeight - viewer.Bounds.Height / 2 + elementHeight / 2);
				}
				else if (e.Key is Key.Up)
				{
					anchorIndex = Math.Max(0, anchorIndex - 1);

					UpdateSelection(anchorIndex);

					viewer.Offset = new Vector(0, anchorIndex * elementHeight - viewer.Bounds.Height / 2 + elementHeight / 2);
				}
				else if (e.Key.ToString() is { Length: 1 } key)
				{
					var index = IndexOf(Char.ToUpper(key[0]));

					if (index != -1)
					{
						anchorIndex = index;

						UpdateSelection(anchorIndex);

						viewer.Offset = new Vector(0, anchorIndex * elementHeight - viewer.Bounds.Height / 2 + elementHeight / 2);
					}
				}
			}

			int IndexOf(char startChar)
			{
				var files = Files;

				for (int i = 0; i < files.Count; i++)
				{
					if (Char.ToUpper(files[i].Name[0]) == startChar)
					{
						return i;
					}
				}

				return -1;
			}
		}

		private void Grid_ElementClearing(object? sender, ItemsRepeaterElementClearingEventArgs e)
		{
			if (e.Element is ListBoxItem { DataContext: FileModel model } item)
			{
				item.DoubleTapped -= Item_DoubleTapped;
				item.PointerPressed -= Item_PointerPressed;
			}
		}

		private void Item_PointerPressed(object? sender, PointerPressedEventArgs e)
		{
			if (sender is ListBoxItem { DataContext: FileModel model } item)
			{
				var point = e.GetCurrentPoint(item);

				if (point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed)
				{
					var index = Files.IndexOf(model);

					UpdateSelection(
												index,
												true,
												e.KeyModifiers.HasAllFlags(KeyModifiers.Shift),
												e.KeyModifiers.HasAllFlags(KeyModifiers.Control));
				}
			}
		}

		private void Grid_ElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
		{
			if (e.Element is ListBoxItem { DataContext: FileModel model } item)
			{
				item.DoubleTapped += Item_DoubleTapped;
				item.PointerPressed += Item_PointerPressed;
			}
		}

		private void Item_DoubleTapped(object? sender, RoutedEventArgs e)
		{
			if (sender is DataGridRow { DataContext: FileModel model })
			{
				PathChanged(model.Path);
			}
		}

		private void Model_SelectionChanged(FileModel obj)
		{
			obj.OnPropertyChanged(nameof(obj.IsSelected));
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
				bool toggleModifier = false)
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
				for (int i = 0; i < files.Count; i++)
				{
					files[i].IsSelected = false;
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
				files[index].IsSelected ^= true;
			}
			else
			{
				for (int i = 0; i < files.Count; i++)
				{
					files[i].IsSelected = false;
				}

				files[index].IsSelected = true;
			}

			if (!range)
			{
				anchorIndex = index;
			}

			files.PropertyChanged("IsSelected");
		}

		protected void OnPropertyChanged<T>(ref T property, T value, [CallerMemberName] string name = null)
		{
			property = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		protected void OnPropertyChanged<T>(T property, T value, [CallerMemberName] string name = null)
		{
			property = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		public void OnPropertyChanged(string name)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}
}