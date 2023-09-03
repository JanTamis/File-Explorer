using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Runtime;
using CommunityToolkit.Mvvm.Input;
using FileExplorer.Models;
using FileExplorer.ViewModels;

namespace FileExplorer.Views;

public sealed partial class MainWindow : FluentWindow
{
	public MainWindow()
	{
		InitializeComponent();

		// var grid = this.FindControl<ListBox>("fileGrid");
		// var tree = FolderTree;
		// var pathFolders = this.FindControl<Menu>("pathFolders");
		// var searchBar = this.searchBar;
		//
		// if (grid is not null)
		// {
		// 	grid.ContainerPrepared += ItemContainerGenerator_Materialized;
		// }
		//
		// if (tree is not null)
		// {
		// 	tree.SelectionChanged += Tree_SelectionChanged;
		//
		// 	DataContextChanged += delegate
		// 	{
		// 		if (DataContext is MainWindowViewModel viewModel)
		// 		{
		// 			tree.ItemsSource = viewModel.Folders;
		//
		// 			KeyUp += (_, args) =>
		// 			{
		// 				if (args.KeyModifiers is KeyModifiers.Control && args.Key is Key.A && !viewModel.CurrentTab.PopupVisible)
		// 				{
		// 					viewModel.CurrentTab.DisplayControl.SelectAll();
		// 				}
		// 			};
		// 		}
		// 	};
		//
		// 	tree.ContainerPrepared += TreeItemGenerated;
		//
		// 	TreeViewItem.IsExpandedProperty.Changed.Subscribe(async x =>
		// 	{
		// 		if (x.Sender is TreeViewItem { DataContext: FolderModel folderModel, ItemsSource: FolderModel[] } treeItem)
		// 		{
		// 			treeItem.ItemsSource = await Task.Run(() => folderModel.SubSegments.ToArray());
		// 		}
		// 	});
		// }
		//
		// if (pathFolders is not null)
		// {
		// 	pathFolders.ContainerPrepared += ItemContainerGenerator_Materialized1;
		// }
		//
		// if (this.FindControl<Border>("FileCountLabel") is { } label)
		// {
		// 	label.PointerEntered += delegate
		// 	{
		// 		if (DataContext is MainWindowViewModel { CurrentTab: { } tab })
		// 		{
		// 			tab.PointerOverAmount = true;
		// 		}
		// 	};
		//
		// 	label.PointerExited += delegate
		// 	{
		// 		if (DataContext is MainWindowViewModel { CurrentTab: { } tab })
		// 		{
		// 			tab.PointerOverAmount = false;
		// 		}
		// 	};
		// }
		//
		//
		searchBar.KeyUp += SearchBar_KeyUp;
		// PointerPressed += MainWindow_PointerPressed;
	}

	private void TreeItemGenerated(object? sender, ContainerPreparedEventArgs e)
	{
		if (e.Container is TreeViewItem { DataContext: FolderModel folderModel, } treeItem)
		{
			treeItem.ContainerPrepared += TreeItemGenerated;

			if (folderModel.HasItems || folderModel.TreeItem is null)
			{
				treeItem.ItemsSource = new[] { new FolderModel(new FileSystemTreeItem("Loading...", false)), };
			}
		}
	}

	private async void SearchBar_KeyUp(object? sender, KeyEventArgs e)
	{
		if (sender is TextBox && e.Key is Key.Enter && DataContext is MainWindowViewModel model)
		{
			await model.StartSearch();
		}
	}

	private void OnTabCloseClick(object sender, RoutedEventArgs e)
	{
		if (DataContext is MainWindowViewModel { Tabs.Count: > 0, } model && sender is Button { DataContext: TabItemViewModel tab, })
		{
			tab.CancelUpdateFiles();
			model.Tabs.Remove(tab);

			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
		}
	}

	private void ItemContainerGenerator_Materialized1(object? sender, ContainerPreparedEventArgs e)
	{
		if (e.Container is MenuItem { DataContext: FolderModel folder, ContextMenu: not null, } item && DataContext is MainWindowViewModel model)
		{
			item.Command = new RelayCommand(() => { model.CurrentTab.CurrentFolder = new FileModel(folder.TreeItem); });

			item.ContextMenu.ContainerPrepared += (_, ee) =>
			{
				if (ee.Container is MenuItem menuItem)
				{
					menuItem.Tapped += delegate { model.CurrentTab.CurrentFolder = new FileModel(folder.TreeItem); };
				}
			};
		}
	}

	private void ItemContainerGenerator_Materialized(object? sender, ContainerPreparedEventArgs e)
	{
		if (DataContext is MainWindowViewModel model)
		{
			if (e.Container is ListBoxItem { DataContext: FileModel fileModel, } item)
			{
				item.DoubleTapped += async delegate { await model.SetPath(fileModel.TreeItem); };
			}
		}
	}

	private async void MainWindow_PointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (DataContext is MainWindowViewModel model && e.Pointer.Type is PointerType.Mouse)
		{
			var properties = e.GetCurrentPoint(this).Properties;

			if (properties.IsXButton1Pressed)
			{
				await model.Undo();
			}
			else if (properties.IsXButton2Pressed)
			{
				await model.Redo();
			}
		}
	}

	private async void Tree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (DataContext is MainWindowViewModel model && e.AddedItems is [FolderModel { TreeItem: not null, } folderModel, ..,])
		{
			await model.SetPath(folderModel.TreeItem);
		}
	}

	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);

		Environment.Exit(0);
	}
}