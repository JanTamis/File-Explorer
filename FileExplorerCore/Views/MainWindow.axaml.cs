using System;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileExplorerCore.Models;
using FileExplorerCore.ViewModels;
using ReactiveUI;
using System.Runtime;
using System.Linq;
using System.Threading.Tasks;

namespace FileExplorerCore.Views
{
	public class MainWindow : FluentWindow
	{
		public MainWindow()
		{
			InitializeComponent();
#if DEBUG
			// this.AttachDevTools();
#endif
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);

			var grid = this.FindControl<ListBox>("fileGrid");
			var tree = this.FindControl<TreeView>("FolderTree");
			var pathFolders = this.FindControl<Menu>("pathFolders");
			var searchBar = this.FindControl<TextBox>("searchBar");

			if (grid is not null)
			{
				grid.ItemContainerGenerator.Materialized += ItemContainerGenerator_Materialized;
			}

			if (tree is not null)
			{
				tree.SelectionChanged += Tree_SelectionChanged;

				DataContextChanged += delegate
				{
					if (DataContext is MainWindowViewModel viewModel)
					{
						tree.Items = viewModel.Folders;
					}
				};

				tree.ItemContainerGenerator.Materialized += TreeItemGenerated;

				TreeViewItem.IsExpandedProperty.Changed.Subscribe(async x =>
				{
					if (x.Sender is TreeViewItem { DataContext: FolderModel folderModel } treeItem && (treeItem.Items is FolderModel[]))
					{
						treeItem.Items = await Task.Run(() => folderModel.SubFolders.ToArray());
					}
				});
			}

			if (pathFolders is not null)
			{
				pathFolders.ItemContainerGenerator.Materialized += ItemContainerGenerator_Materialized1;
			}

			searchBar.KeyUp += SearchBar_KeyUp;
			PointerPressed += MainWindow_PointerPressed;
		}

		private async void TreeItemGenerated(object? sender, ItemContainerEventArgs e)
		{
			for (int i = 0; i < e.Containers.Count; i++)
			{
				if (e.Containers[i] is { ContainerControl: TreeViewItem { DataContext: FolderModel folderModel } treeItem })
				{
					treeItem.ItemContainerGenerator.Materialized += TreeItemGenerated;

          if (folderModel.HasFolders || folderModel.TreeItem is null)
          {
            treeItem.Items = new FolderModel[] { new FolderModel(new FileSystemTreeItem("Loading...", false)) }; 
          }
					
					if (OperatingSystem.IsWindows() && treeItem.Parent is TreeView)
					{
						treeItem.Margin = new Avalonia.Thickness(0, 0, 0, 15);
						treeItem.IsExpanded = true;
						await Task.Delay(100);
					}
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
			if (DataContext is MainWindowViewModel model && sender is Button { DataContext: TabItemViewModel tab })
			{
				if (model.Tabs.Count > 1)
				{
					tab.CancelUpdateFiles();
					model.Tabs.Remove(tab);

					GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);
				}
			}
		}

		private void ItemContainerGenerator_Materialized1(object? sender, ItemContainerEventArgs e)
		{
			foreach (var info in e.Containers)
			{
				if (info.ContainerControl is MenuItem { DataContext: FolderModel folder, ContextMenu: not null } item && DataContext is MainWindowViewModel model)
				{
					item.Command = ReactiveCommand.Create(() =>
					{
						model.CurrentTab.TreeItem = folder.TreeItem;
					});

					item.ContextMenu.ItemContainerGenerator.Materialized += (_, ee) =>
					{
						foreach (var info in ee.Containers)
						{
							if (info is { ContainerControl: MenuItem menuItem, Item: FolderModel folderModel })
							{
								menuItem.Tapped += delegate
								{
									model.CurrentTab.TreeItem = folderModel.TreeItem;
								};
							}
						}
					};
				}
			}
		}

		private void ItemContainerGenerator_Materialized(object? sender, ItemContainerEventArgs e)
		{
			if (DataContext is MainWindowViewModel model)
			{
				foreach (var container in e.Containers)
				{
					if (container.ContainerControl is ListBoxItem { DataContext: FileModel fileModel } item)
					{
						item.DoubleTapped += async delegate { await model.SetPath(fileModel.TreeItem); };
					}
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
			if (DataContext is MainWindowViewModel model && e.AddedItems is [FolderModel { TreeItem: not null } folderModel, ..])
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
}
