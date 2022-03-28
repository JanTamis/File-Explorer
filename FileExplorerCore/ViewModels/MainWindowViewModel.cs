using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.IO.Compression;
using System.Linq;
using System.Timers;
using System.Threading.Tasks;
using Avalonia;
using static FileExplorerCore.Helpers.SpanSplitExtensions;

namespace FileExplorerCore.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		public readonly WindowNotificationManager NotificationManager;

		private TabItemViewModel _currentTab;

		public static IEnumerable<SortEnum> SortValues => Enum.GetValues<SortEnum>();

		public ObservableRangeCollection<FolderModel> Folders { get; set; }

		//public static Tree<FileSystemTreeItem, string>? Tree { get; set; }

		public ObservableRangeCollection<FileModel> Files => CurrentTab.Files;

		public ObservableCollection<TabItemViewModel> Tabs { get; } = new();

		public IEnumerable<string> SearchHistory => CurrentTab.TreeItem is not null
			? Files
				.Where(w => !w.IsFolder)
				.GroupBy(g => g.Extension)
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

		public MainWindowViewModel(WindowNotificationManager manager)
		{
			NotificationManager = manager;
			
			//if (OperatingSystem.IsWindows() && (Tree is null || Tree.Children.Count != DriveInfo.GetDrives().Count(a => a.IsReady)))
			//{
			//	Tree = new Tree<FileSystemTreeItem, string>(DriveInfo
			//		.GetDrives()
			//		.Where(w => w.IsReady)
			//		.Select(s => new FileSystemTreeItem(s.RootDirectory.FullName, true)));
			//}
			//else if (OperatingSystem.IsMacOS() && (Tree is null || Tree.Children.Count != 1))
			//{
			//	Tree = new Tree<FileSystemTreeItem, string>(new[] { new FileSystemTreeItem("/", true) });
			//}

			if (OperatingSystem.IsWindows())
			{
				var drives = DriveInfo
					.GetDrives()
					.Where(w => w.IsReady)
					.Select(s => new FolderModel(new FileSystemTreeItem(s.Name, true)));

				var quickAccess = from specialFolder in Enum.GetValues<KnownFolder>()
													select new FolderModel(GetTreeItem(KnownFolders.GetPath(specialFolder)));

				Folders = new ObservableRangeCollection<FolderModel>(quickAccess.Concat(drives), true);
			}
			else
			{
				Folders = new ObservableRangeCollection<FolderModel>(new[] 
				{ 
					new FolderModel(new FileSystemTreeItem(new string(PathHelper.DirectorySeparator, 1), true)) 
				});
			}

			AddTab();
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
			await CurrentTab.SetPath(CurrentTab?.TreeItem?.Parent);
		}

		public async ValueTask StartSearch()
		{
			if (CurrentTab.Search is { Length: > 0 } && CurrentTab.TreeItem is not null)
			{
				await CurrentTab.UpdateFiles(true, CurrentTab.Search);
			}
		}

		public async ValueTask Undo()
		{
			await CurrentTab.SetPath(CurrentTab.Undo());
		}

		public async ValueTask Redo()
		{
			await CurrentTab.SetPath(CurrentTab.Redo());
		}

		public void CancelUpdateFiles()
		{
			CurrentTab.CancelUpdateFiles();
		}

		public async ValueTask SetPath(FileSystemTreeItem path)
		{
			await CurrentTab.SetPath(path);
		}

		public async ValueTask Refresh()
		{
			await CurrentTab.SetPath(CurrentTab.TreeItem);
		}

		public async ValueTask SelectAll()
		{
			foreach (var file in CurrentTab.Files)
			{
				file.IsSelected = true;
			}

			await CurrentTab.OnPropertyChanged(nameof(CurrentTab.SelectionText));
			CurrentTab.Files.PropertyChanged("IsSelected");
		}

		public async ValueTask SelectNone()
		{
			foreach (var file in CurrentTab.Files)
			{
				file.IsSelected = false;
			}

			await CurrentTab.OnPropertyChanged(nameof(CurrentTab.SelectionText));
			CurrentTab.Files.PropertyChanged("IsSelected");
		}

		public async ValueTask SelectInvert()
		{
			foreach (var file in CurrentTab.Files)
			{
				file.IsSelected ^= true;
			}

			await CurrentTab.OnPropertyChanged(nameof(CurrentTab.SelectionText));
			CurrentTab.Files.PropertyChanged("IsSelected");
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
			if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null && CurrentTab.SelectionCount > 0)
			{
				var fileIndex = CurrentTab.Files.IndexOf(CurrentTab.Files.FirstOrDefault(x => x.IsSelected)!);

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

		public void ZipFiles()
		{
			if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null)
			{
				var zip = new Zip
				{
					SelectedFiles = Files.Where(w => w.IsSelected),
					TreeItem = CurrentTab.TreeItem,
					CompressionLevel = CompressionLevel.Optimal,
				};

				CurrentTab.PopupContent = zip;

				zip.ZipFiles();
				
				zip.OnClose += () =>
				{
					if (zip.FileModel is not null)
					{
						CurrentTab.Files.Add(zip.FileModel);
					}
				};
			}
		}

		public async Task CopyFiles()
		{
			var data = new DataObject();
			data.Set(DataFormats.FileNames, CurrentTab.Files.Where(x => x.IsSelected)
				.Select(s => s.Path)
				.ToArray());

			await Application.Current!.Clipboard!.SetDataObjectAsync(data);

			NotificationManager.Show(new Notification("Copy Files", "Files has been copied"));
		}

		public async void CopyPath()
		{
			await Application.Current!.Clipboard!.SetTextAsync(CurrentTab.TreeItem.GetPath(x => x.ToString()));

			NotificationManager.Show(new Notification("Copy Path", "The path has been copied"));
		}

		public void DeleteFiles()
		{
			var selectedFiles = CurrentTab.Files.Where(x => x.IsSelected);
			var selectedFileCount = CurrentTab.SelectionCount;

			if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null && selectedFileCount > 0)
			{
				var choice = new Choice
				{
					CloseText = "Cancel",
					SubmitText = "Delete",
					Message = CultureInfo.CurrentCulture.TextInfo.ToTitleCase($"Are you sure you want to delete {selectedFileCount} item{(selectedFileCount > 1 ? "s" : String.Empty)}?"),
				};

				choice.OnSubmit += () =>
				{
					var deletedFiles = new List<FileModel>(selectedFileCount);

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
				var selector = new TabSelector
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

			CurrentTab.TokenSource?.Cancel();
			CurrentTab.TokenSource = new System.Threading.CancellationTokenSource();

			var rootTask = Task.Run(async () =>
			{
				var query = CurrentTab.TreeItem.EnumerateChildren()
					.Select(s => new FileIndexModel(s));

				var comparer = new AsyncComparer<FileIndexModel>(async (x, y) =>
				{
					var resultX = await x.TaskSize;
					var resultY = await y.TaskSize;

					return resultY.CompareTo(resultX);
				});

				await view.Root.AddRangeAsync(query, comparer, token: CurrentTab.TokenSource.Token);
			}, CurrentTab.TokenSource.Token);

			var extensionTask = Task.Run(async () =>
			{
				await Dispatcher.UIThread.InvokeAsync(() => CurrentTab.IsLoading = true);

				var extensionQuery = CurrentTab.TreeItem
					.EnumerateChildren()
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
			if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null)
			{
				var model = CurrentTab.Files.FirstOrDefault(x => x.IsSelected);

				if (model is not null)
				{
					var properties = new Properties
					{
						Model = model,
					};

					CurrentTab.PopupContent = properties;
				}
			}
		}

		public static FileSystemTreeItem GetTreeItem(ReadOnlySpan<char> path)
		{
			FileSystemTreeItem item = null!;
			Enumerable1<char> enumerable = new();

			if (OperatingSystem.IsMacOS())
			{
				item = new FileSystemTreeItem(path[..1], true);
				enumerable = new Enumerable1<char>(path[1..], PathHelper.DirectorySeparator);
			}
			else if (OperatingSystem.IsWindows())
			{
				item = new FileSystemTreeItem(path[..3], true);
				enumerable = new Enumerable1<char>(path[3..], PathHelper.DirectorySeparator);
			}

			var enumerator = enumerable.GetEnumerator();

			while (enumerator.MoveNext())
			{
				item = new FileSystemTreeItem(enumerator.Current, true, item);
			}

			return item;
		}
	}
}