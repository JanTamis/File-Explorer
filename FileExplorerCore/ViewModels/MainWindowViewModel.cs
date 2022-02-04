using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Threading;
using FileExplorerCore.DisplayViews;
using FileExplorerCore.Helpers;
using FileExplorerCore.Models;
using FileExplorerCore.Popup;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Timers;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ProtoBuf;

namespace FileExplorerCore.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		public readonly WindowNotificationManager NotificationManager;

		private TabItemViewModel _currentTab;
		private IEnumerable<string> searchHistory;

		public static IEnumerable<SortEnum> SortValues => Enum.GetValues<SortEnum>();

		public static ObservableRangeCollection<FolderModel> Folders { get; set; }

		public static Tree<FileSystemTreeItem, string>? Tree { get; set; }

		public ObservableRangeCollection<FileModel> Files => CurrentTab.Files;

		public ObservableRangeCollection<TabItemViewModel> Tabs { get; set; } = new();

		private FileSystemWatcher watcher;

		public IEnumerable<string> SearchHistory => CurrentTab.TreeItem is not null
			? CurrentTab.TreeItem.EnumerateChildrenWithoutInitialize()
				.Cast<FileSystemTreeItem>()
				.Where(w => !w.IsFolder)
				.GroupBy(g => Path.GetExtension(g.Value))
				.Where(w => !String.IsNullOrWhiteSpace(w.Key))
				.OrderBy(o => o.Key)
				.Select(s => "*" + s.Key)
			: Enumerable.Empty<string>();

		public TabItemViewModel CurrentTab
		{
			get => _currentTab;
			set
			{
				OnPropertyChanged(ref _currentTab, value);

				OnPropertyChanged(nameof(Files));
				OnPropertyChanged(nameof(Path));
			}
		}

		// public string Path
		// {
		// 	get => CurrentTab.TreeItem?.GetPath(x => x.ToString());
		// 	set
		// 	{
		// 		if (value == Path)
		// 			return;
		//
		// 		CurrentTab.TreeItem = GetTreeItemInitialized(value);
		//
		// 		OnPropertyChanged();
		//
		// 		CurrentTab.UpdateFiles(false, "*");
		// 	}
		// }

		public MainWindowViewModel(WindowNotificationManager manager)
		{
			NotificationManager = manager;

			Serializer.PrepareSerializer<Tree<FileSystemTreeItem, string>>();

			var path = Path.Combine(Environment.CurrentDirectory, "Index.bin");

			if (File.Exists(path))
			{
				using (var stream = File.OpenRead(path))
				{
					Tree = Serializer.Deserialize<Tree<FileSystemTreeItem, string>>(stream);

					foreach (var child in Tree.Children)
					{
						SetParents(child);
					}
				}
			}

			if (OperatingSystem.IsWindows() && (Tree is null || Tree.Children.Count != DriveInfo.GetDrives().Count(a => a.IsReady)))
			{
				Tree = new Tree<FileSystemTreeItem, string>(DriveInfo
					.GetDrives()
					.Where(w => w.IsReady)
					.Select(s => new FileSystemTreeItem(s.RootDirectory.FullName, true)));
			}
			else if (OperatingSystem.IsMacOS() && (Tree is null || Tree.Children.Count != 1))
			{
				Tree = new Tree<FileSystemTreeItem, string>(new[] { new FileSystemTreeItem("/", true) });
			}

			if (OperatingSystem.IsWindows() && Tree is not null)
			{
				var drives = Tree.Children
					.Select(s => new FolderModel(s));

				var quickAccess = from specialFolder in Enum.GetValues<KnownFolder>()
					select new FolderModel(GetTreeItemInitialized(KnownFolders.GetPath(specialFolder).ToString()));

				Folders = new ObservableRangeCollection<FolderModel>(quickAccess.Concat(drives));
			}
			else if (OperatingSystem.IsMacOS() && Tree is not null)
			{
				Folders = new ObservableRangeCollection<FolderModel>(Tree.Children
					.Select(s => new FolderModel(s)));
			}

			AddTab();

			watcher = new FileSystemWatcher("/", "*");

			watcher.Created += Watcher_Created;
			watcher.Deleted += Watcher_Deleted;
			watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
			watcher.IncludeSubdirectories = true;
			watcher.EnableRaisingEvents = true;

			if (Application.Current?.ApplicationLifetime is ClassicDesktopStyleApplicationLifetime applicationLifetime)
			{
				applicationLifetime.Exit += delegate
				{
					var path = Path.Combine(Environment.CurrentDirectory, "Index.bin");

					if (File.Exists(path))
					{
						File.Delete(path);
					}

					using (var stream = File.OpenWrite(path))
					{
						Serializer.Serialize(stream, Tree);
					}
				};
			}
		}

		private void SetParents<T>(TreeItem<T> item)
		{
			foreach (var child in item.EnumerateChildrenWithoutInitialize())
			{
				child.Parent = item;
				SetParents(child);
			}
		}

		public void Testing()
		{
			var count = 0;
			var watch = Stopwatch.StartNew();
			var time = DateTime.Now;

			foreach (var _ in Tree.EnumerateChildren())
			{
				count++;

				if ((DateTime.Now - time).TotalSeconds >= 1)
				{
					Debug.WriteLine(count.ToString("N0"));
					time = DateTime.Now;
				}
			}

			watch.Stop();

			Debug.WriteLine(watch.Elapsed);
			Debug.WriteLine(count);
		}

		private void Watcher_Deleted(object sender, FileSystemEventArgs e)
		{
			// Debug.WriteLine("Deleted: " + e.FullPath);

			//var item = GetTreeItem(e.FullPath);

			//if (!item.IsFolder && item.GetPath((path, filePath) => path.SequenceEqual(filePath), e.FullPath))
			//{
			//	item.Remove();
			//}
		}

		private void Watcher_Created(object sender, FileSystemEventArgs e)
		{
			// Debug.WriteLine("Created: " + e.FullPath);

			//var item = GetTreeItem(e.FullPath);

			//if (item.IsFolder && item.GetPath((path, filePath) => System.IO.Path.GetDirectoryName(path).SequenceEqual(filePath), System.IO.Path.GetDirectoryName(e.FullPath)))
			//{
			//	item.Children.Add(new FileSystemTreeItem(System.IO.Path.GetFileName(e.FullPath), false, item));
			//}
		}

		private async ValueTask Timer_Elapsed(object sender, ElapsedEventArgs e)
		{
			await StartSearch();
		}

		public void AddTab()
		{
			var tab = new TabItemViewModel();

			Tabs.Add(tab);
			CurrentTab = tab;

			tab.PathChanged += () => OnPropertyChanged(nameof(SearchHistory));
		}

		public async Task GoUp()
		{
			await CurrentTab.SetPath(CurrentTab?.TreeItem?.Parent as FileSystemTreeItem);
		}

		public async ValueTask StartSearch()
		{
			if (CurrentTab.Search is { Length: > 0 } && CurrentTab.TreeItem is not null)
			{
				CurrentTab.IsSearching = true;

				await CurrentTab.UpdateFiles(CurrentTab.IsSearching, CurrentTab.Search);
			}
		}

		public async ValueTask Undo()
		{
			CurrentTab.TreeItem = CurrentTab.Undo();
			await CurrentTab.UpdateFiles(false, "*");
		}

		public async ValueTask Redo()
		{
			CurrentTab.TreeItem = CurrentTab.Redo();
			await CurrentTab.UpdateFiles(false, "*");
		}

		public void CancelUpdateFiles()
		{
			CurrentTab.CancelUpdateFiles();
		}

		public async ValueTask SetPath(FileSystemTreeItem path)
		{
			if (CurrentTab is not null)
			{
				await CurrentTab.SetPath(path);
			}
		}

		public async ValueTask Refresh()
		{
			if (CurrentTab.TreeItem is not null)
			{
				await CurrentTab.UpdateFiles(CurrentTab.IsSearching, CurrentTab.IsSearching ? CurrentTab.Search : "*");
			}
		}

		public void SelectAll()
		{
			if (CurrentTab is not null)
			{
				foreach (var file in CurrentTab.Files)
				{
					file.IsSelected = true;
				}

				CurrentTab.OnPropertyChanged(nameof(CurrentTab.SelectionText));
				CurrentTab.Files.PropertyChanged("IsSelected");
			}
		}

		public void SelectNone()
		{
			if (CurrentTab is not null)
			{
				foreach (var file in CurrentTab.Files)
				{
					file.IsSelected = false;
				}

				CurrentTab.OnPropertyChanged(nameof(CurrentTab.SelectionText));
				CurrentTab.Files.PropertyChanged("IsSelected");
			}
		}

		public void SelectInvert()
		{
			if (CurrentTab is not null)
			{
				foreach (var file in CurrentTab.Files)
				{
					file.IsSelected ^= true;
				}

				CurrentTab.OnPropertyChanged(nameof(CurrentTab.SelectionText));
				CurrentTab.Files.PropertyChanged("IsSelected");
			}
		}

		public void ShowSettings()
		{
			if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null)
			{
				CurrentTab.PopupContent = new Settings();
			}
		}

		public void Rename()
		{
			if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null)
			{
				var fileIndex = CurrentTab.Files.IndexOf(CurrentTab.Files.FirstOrDefault(x => x.IsSelected));

				if (fileIndex is not -1)
				{
					var rename = new Rename
					{
						Files = CurrentTab.Files,
						Index = fileIndex,
					};

					CurrentTab.PopupContent = rename;

					rename.OnPropertyChanged(nameof(rename.File));
				}
			}
		}

		public async void CopyFiles()
		{
			var data = new DataObject();
			data.Set(DataFormats.FileNames, CurrentTab.Files.Where(x => x.IsSelected)
				.Select(s => s.Path)
				.ToArray());

			await Application.Current.Clipboard.SetDataObjectAsync(data);

			NotificationManager.Show(new Notification("Copy Files", "Files has been copied"));
		}

		public async void CopyPath()
		{
			await Application.Current.Clipboard.SetTextAsync(CurrentTab.TreeItem.GetPath(x => x.ToString()));

			NotificationManager.Show(new Notification("Copy Path", "The path has been copied"));
		}

		public void DeleteFiles()
		{
			var selectedFiles = CurrentTab.Files.Where(x => x.IsSelected);
			var SelectedFileCount = selectedFiles.Count();

			if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null && SelectedFileCount > 0)
			{
				var choice = new Choice
				{
					CloseText = "Cancel",
					SubmitText = "Delete",
					Message = CultureInfo.CurrentCulture.TextInfo.ToTitleCase($"Are you sure you want to delete {SelectedFileCount} item{(SelectedFileCount > 1 ? "s" : String.Empty)}?"),
				};

				choice.OnSubmit += () =>
				{
					var deletedFiles = new List<FileModel>(SelectedFileCount);

					foreach (var file in selectedFiles)
					{
						try
						{
							if (File.Exists(file.Path))
							{
								FileSystem.DeleteFile(file.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
							}
							else if (Directory.Exists(file.Path))
							{
								FileSystem.DeleteDirectory(file.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
							}

							deletedFiles.Add(file);
						}
						catch (Exception)
						{
						}
					}

					CurrentTab.Files.RemoveRange(deletedFiles);
					CurrentTab.PopupContent = null;
				};

				CurrentTab.PopupContent = choice;
			}
		}

		public void CopyTo()
		{
			if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null && Tabs.Count(x => !String.IsNullOrWhiteSpace(x.TreeItem.GetPath(x => x.ToString()))) > 1)
			{
				var selector = new TabSelector()
				{
					Tabs = new ObservableRangeCollection<TabItemViewModel>(Tabs.Where(x => x != CurrentTab && !String.IsNullOrWhiteSpace(x.TreeItem.GetPath(x => x.ToString())))),
				};

				selector.TabSelectionChanged += _ => { selector.Close(); };

				CurrentTab.PopupContent = selector;
			}
		}

		public async Task AnalyzeFolder()
		{
			var view = new AnalyzerView();
			CurrentTab.DisplayControl = view;

			var rootTask = Task.Run(async () =>
			{
				var query = CurrentTab.TreeItem.EnumerateChildren()
					.Cast<FileSystemTreeItem>()
					.Select(s => new FileIndexModel(s));

				var comparer = new AsyncComparer<FileIndexModel>(async (x, y) =>
				{
					var resultX = await x.TaskSize;
					var resultY = await y.TaskSize;

					return resultY.CompareTo(resultX);
				});

				await view.Root.AddRange(query, comparer, token: CurrentTab.TokenSource.Token);
			});

			var extensionTask = Task.Run(async () =>
			{
				await Dispatcher.UIThread.InvokeAsync(() => CurrentTab.IsLoading = true);

				var extensionQuery = CurrentTab.TreeItem
					.EnumerateChildren()
					.Cast<FileSystemTreeItem>()
					.Where(w => !w.IsFolder)
					.GroupBy(g => Path.GetExtension(g.Value));

				var comparer = new ExtensionModelComparer();

				foreach (var extension in extensionQuery)
				{
					if (CurrentTab.TokenSource.IsCancellationRequested)
						break;

					if (!String.IsNullOrEmpty(extension.Key))
					{
						var model = new ExtensionModel(extension.Key, extension.Sum(s => new FileInfo(s.GetPath(path => path.ToString())).Length))
						{
							TotalFiles = extension.Count(),
						};

						var index = view.Extensions.BinarySearch(model, comparer);

						if (index >= 0)
						{
							await Dispatcher.UIThread.InvokeAsync(() => view.Extensions.Insert(index, model));
						}
						else
						{
							await Dispatcher.UIThread.InvokeAsync(() => view.Extensions.Insert(~index, model));
						}
					}
				}

				await Dispatcher.UIThread.InvokeAsync(() => CurrentTab.IsLoading = false);
			});

			await Task.WhenAll(rootTask, extensionTask);
		}

		public void ShowProperties()
		{
			//if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null)
			//{
			//	var model = CurrentTab.Files.FirstOrDefault(x => x.IsSelected) ?? new FileModel(Path, true);

			//	var properties = new Properties()
			//	{
			//		Model = model,
			//	};

			//	CurrentTab.PopupContent = properties;
			//}
		}

		private FileSystemTreeItem? GetTreeItem(string? path)
		{
			if (path is null)
			{
				return null;
			}

			string[] temp = null;
			FileSystemTreeItem item = null;

			if (OperatingSystem.IsMacOS())
			{
				item = Tree.Children[0];
				temp = path.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			}
			else if (OperatingSystem.IsWindows())
			{
				temp = path.Split(new char[] { '\\' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

				foreach (var child in Tree.EnumerateChildren(0))
				{
					if (child.Value.StartsWith(path[0]))
					{
						item = child;
						break;
					}
				}
			}

			if (item is not null)
			{
				foreach (var split in temp)
				{
					foreach (FileSystemTreeItem child in item.EnumerateChildrenWithoutInitialize())
					{
						if (child.Value == split)
						{
							item = child;
							break;
						}
					}
				}
			}

			if (temp.Length > 0)
			{
				item = GetItem(item, temp, 1);
			}

			return item;

			static FileSystemTreeItem GetItem(FileSystemTreeItem item, IReadOnlyList<string> path, int index)
			{
				if (index == path.Count || item is null)
				{
					return item;
				}

				foreach (var child in item.EnumerateChildrenWithoutInitialize())
				{
					if (child is FileSystemTreeItem { IsFolder: true } treeItem && treeItem.Value == path[index])
					{
						return GetItem(treeItem, path, index + 1);
					}
				}

				return item;
			}
		}

		public static FileSystemTreeItem GetTreeItemInitialized(string path)
		{
			string[] temp = null;
			FileSystemTreeItem item = null;

			if (OperatingSystem.IsMacOS())
			{
				item = Tree.Children[0];
				temp = path.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			}
			else if (OperatingSystem.IsWindows())
			{
				temp = path.Split('\\', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

				foreach (var child in Tree.EnumerateChildren(0))
				{
					if (child.Value.StartsWith(path[0]))
					{
						item = child;
						break;
					}
				}
			}

			foreach (var split in temp)
			{
				if (item is not null)
				{
					foreach (FileSystemTreeItem child in item.EnumerateChildren(0))
					{
						if (child.Value == split)
						{
							item = child;
							break;
						}
					}
				}
			}

			if (temp.Length > 0)
			{
				item = GetItem(item, temp, 1);
			}

			return item;

			static FileSystemTreeItem GetItem(FileSystemTreeItem item, IReadOnlyList<string> path, int index)
			{
				if (index == path.Count)
				{
					return item;
				}

				if (item is not null)
				{
					foreach (var child in item.EnumerateChildrenWithoutInitialize())
					{
						if (child is FileSystemTreeItem { IsFolder: true } treeItem && treeItem.Value == path[index])
						{
							return GetItem(treeItem, path, index + 1);
						}
					}
				}

				return item;
			}
		}
	}
}