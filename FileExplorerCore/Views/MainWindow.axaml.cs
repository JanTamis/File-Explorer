using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileExplorerCore.Models;
using FileExplorerCore.ViewModels;
using ReactiveUI;

namespace FileExplorerCore.Views
{
	public class MainWindow : FluentWindow
	{
		public MainWindow()
		{
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif

			var manager = new WindowNotificationManager(this)
			{
				Position = NotificationPosition.BottomLeft,
				MaxItems = 3,
			};
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);

			var grid = this.FindControl<ListBox>("fileGrid");
			var tree = this.FindControl<TreeView>("FolderTree");
			var pathFolders = this.FindControl<Menu>("pathFolders");
			var searchBar = this.FindControl<AutoCompleteBox>("searchBar");

			if (grid is { })
			{
				grid.ItemContainerGenerator.Materialized += ItemContainerGenerator_Materialized;
			}

			if (tree is { })
			{
				tree.SelectionChanged += Tree_SelectionChanged;
			}

			if (pathFolders is { })
			{
				pathFolders.ItemContainerGenerator.Materialized += ItemContainerGenerator_Materialized1;
			}

			searchBar.KeyUp += SearchBar_KeyUp;

			PointerPressed += MainWindow_PointerPressed;
		}

		private void SearchBar_KeyUp(object? sender, KeyEventArgs e)
		{
			if (sender is AutoCompleteBox { IsDropDownOpen: false } && e.Key is Key.Enter && DataContext is MainWindowViewModel model)
			{
				model.StartSearch();
			}
		}

		private void OnButtonClick(object sender, RoutedEventArgs e)
		{
			if (DataContext is MainWindowViewModel model && sender is Button { DataContext: FolderModel folderModel })
			{
				model.CurrentTab.SetPath(folderModel.Path);
			}
		}

		private async void OnComboBoxChanged(object sender, SelectionChangedEventArgs e)
		{
			if (DataContext is MainWindowViewModel model && sender is SelectingItemsControl { SelectedItem: FolderModel folderModel })
			{
				await model.CurrentTab.SetPath(folderModel.Path);
			}
		}

		private void OnTabCloseClick(object sender, RoutedEventArgs e)
		{
			if (DataContext is MainWindowViewModel model && sender is Button { DataContext: TabItemViewModel tab })
			{
				if (model.Tabs.Count > 1)
				{
					model.Tabs.Remove(tab);

					GC.Collect(2, GCCollectionMode.Optimized, false, true);
				}
			}
		}

		private void ItemContainerGenerator_Materialized1(object? sender, ItemContainerEventArgs e)
		{
			foreach (var info in e.Containers)
			{
				if (info.ContainerControl is MenuItem item && item.DataContext is FolderModel folder && DataContext is MainWindowViewModel model)
				{
					item.Command = ReactiveCommand.Create(() =>
					{
						model.Path = folder.Path;
					});

					item.ContextMenu.ItemContainerGenerator.Materialized += (_, ee) =>
					{
						foreach (var info in ee.Containers)
						{
							if (info.ContainerControl is MenuItem item && info.Item is FolderModel folderModel)
							{
								item.Tapped += delegate
								{
									model.CurrentTab.Path = folderModel.Path;
								};
							}
						}
					};
				}
			}
		}

		private void Item_SelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			throw new NotImplementedException();
		}

		private void ItemContainerGenerator_Materialized(object? sender, ItemContainerEventArgs e)
		{
			for (int i = 0; i < e.Containers.Count; i++)
			{
				if (e.Containers[i].ContainerControl is ListBoxItem item)
				{
					item.DoubleTapped += delegate
					{
						if (DataContext is MainWindowViewModel model && item.DataContext is FileModel filemodel)
						{
							model.SetPath(filemodel.Path);
						}
					};
				}
			}
		}

		private void MainWindow_PointerPressed(object? sender, PointerPressedEventArgs e)
		{
			if (DataContext is MainWindowViewModel model && e.Pointer.Type is PointerType.Mouse)
			{
				var properties = e.GetCurrentPoint(this).Properties;

				if (properties.IsXButton1Pressed)
				{
					model.Undo();
				}
				else if (properties.IsXButton2Pressed)
				{
					model.Redo();
				}
			}
		}

		private void Tree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (DataContext is MainWindowViewModel model && e.AddedItems is { Count: > 0 } && e.AddedItems[0] is FolderModel folderModel && folderModel.Path is not "")
			{
				model.SetPath(folderModel.Path);
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);

			Environment.Exit(0);
		}
	}
}
