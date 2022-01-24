using System;
using System.Collections.Generic;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Threading;
using FileExplorerCore.DisplayViews;
using FileExplorerCore.Helpers;
using FileExplorerCore.Models;
using FileExplorerCore.Popup;
using Microsoft.VisualBasic.FileIO;
using Nessos.LinqOptimizer.CSharp;
using System.Globalization;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Timers;
using System.Threading.Tasks;
using System.Diagnostics;
using Avalonia.Controls.Shapes;

namespace FileExplorerCore.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		public readonly WindowNotificationManager notificationManager;

		private TabItemViewModel _currentTab;
		private IEnumerable<string> searchHistory;

		public static IEnumerable<SortEnum> SortValues => Enum.GetValues<SortEnum>();

		public IEnumerable<FolderModel> Folders { get; set; }

		public static Tree<FileSystemTreeItem, string> Tree;

		public ObservableRangeCollection<FileModel> Files => CurrentTab.Files;

		public ObservableRangeCollection<TabItemViewModel> Tabs { get; set; } = new();

		FileSystemWatcher watcher;

		public IEnumerable<string> SearchHistory
		{
			get => searchHistory;
			set => OnPropertyChanged(ref searchHistory, value);
		}

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

		public string Path
		{
			get => CurrentTab.Path;
			set
			{
				if (value == Path) return;
				
				CurrentTab.Path = value;

				OnPropertyChanged();

				CurrentTab.UpdateFiles(false, "*").AsTask().ContinueWith(x =>
				{
					var categories = Enum.GetValues<Categories>().Select(s => s + ":");
					SearchHistory = categories.Concat(CurrentTab.Files.Select(s => "*" + s.Extension).Distinct());
				});
			}
		}

		public MainWindowViewModel(WindowNotificationManager manager)
		{
			notificationManager = manager;

			if (OperatingSystem.IsWindows())
			{
				var drives = from drive in DriveInfo.GetDrives()
										 where drive.IsReady
										 select new FolderModel(drive.RootDirectory.FullName, $"{drive.VolumeLabel} ({drive.Name})");

				var quickAccess = from specialFolder in Enum.GetValues<KnownFolder>()
													select new FolderModel(KnownFolders.GetPath(specialFolder));

				Folders = quickAccess.Concat(drives);
			}
			else if (OperatingSystem.IsMacOS())
			{

			}

			AddTab();

			watcher = new FileSystemWatcher("C://", "*");

			watcher.Created += Watcher_Created;
			watcher.Deleted += Watcher_Deleted;
			watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
			watcher.IncludeSubdirectories = true;
			watcher.EnableRaisingEvents = true;

			Tree = new Tree<FileSystemTreeItem, string>(DriveInfo
				.GetDrives()
				.Where(w => w.DriveType == DriveType.Fixed)
				.Select(s => new FileSystemTreeItem(s.RootDirectory.FullName, null)));
		}

		private void Watcher_Deleted(object sender, FileSystemEventArgs e)
		{
			Debug.WriteLine("Deleted: " + e.FullPath);
		}

		private void Watcher_Created(object sender, FileSystemEventArgs e)
		{
			Debug.WriteLine("Created: " + e.FullPath);
		}

		private void Timer_Elapsed(object sender, ElapsedEventArgs e)
		{
			StartSearch();
		}

		public void AddTab()
		{
			var tab = new TabItemViewModel();

			Tabs.Add(tab);
			CurrentTab = tab;
		}

		public void GoUp()
		{
			if (Path is { Length: > 0 })
			{
				var directory = new DirectoryInfo(Path);

				Path = directory.Parent?.FullName ?? String.Empty;
			}
		}

		public async ValueTask StartSearch()
		{
			if (CurrentTab.Search is { Length: > 0 } && Path is { Length: > 0 })
			{
				CurrentTab.IsSearching = true;

				await CurrentTab.UpdateFiles(CurrentTab.IsSearching, CurrentTab.Search);
			}
		}

		public async ValueTask Undo()
		{
			CurrentTab.Path = CurrentTab.Undo();
			await CurrentTab.UpdateFiles(false, "*");
		}

		public async ValueTask Redo()
		{
			CurrentTab.Path = CurrentTab.Redo();
			await CurrentTab.UpdateFiles(false, "*");
		}

		public void CancelUpdateFiles()
		{
			CurrentTab.CancelUpdateFiles();
		}

		public async ValueTask SetPath(string path)
		{
			if (CurrentTab is not null)
			{
				await CurrentTab.SetPath(path);
			}
		}

		public async ValueTask Refresh()
		{
			if (!String.IsNullOrWhiteSpace(Path))
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

			await App.Current.Clipboard.SetDataObjectAsync(data);

			notificationManager.Show(new Notification("Copy Files", "Files has been copied"));
		}

		public async void CopyPath()
		{
			await App.Current.Clipboard.SetTextAsync(CurrentTab.Path);

			notificationManager.Show(new Notification("Copy Path", "The path has been copied"));
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
						catch (Exception) { }
					}

					CurrentTab.Files.RemoveRange(deletedFiles);
					CurrentTab.PopupContent = null;
				};

				CurrentTab.PopupContent = choice;
			}
		}

		public void CopyTo()
		{
			if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null && Tabs.Count(x => !String.IsNullOrWhiteSpace(x.Path)) > 1)
			{
				var selector = new TabSelector()
				{
					Tabs = new ObservableRangeCollection<TabItemViewModel>(Tabs.Where(x => x != CurrentTab && !String.IsNullOrWhiteSpace(x.Path))),
				};

				selector.TabSelectionChanged += (tab) =>
				{
					selector.Close();
				};

				CurrentTab.PopupContent = selector;
			}
		}

		public void AnalyzeFolder()
		{
			var view = new AnalyzerView();
			CurrentTab.DisplayControl = view;

			var drive = new DriveInfo(Path.Substring(0, 1));
			var parent = new FileIndexModel(Path, true, 0);

			if (drive.Name == Path)
			{
				parent = new FileIndexModel(Path, true, drive.TotalSize - drive.TotalFreeSpace);
			}

			var options = new EnumerationOptions()
			{
				IgnoreInaccessible = true,
				AttributesToSkip = FileAttributes.Temporary,
			};

			var folderQuery = new FileSystemEnumerable<FileIndexModel>(Path, (ref FileSystemEntry x) => new FileIndexModel(x.FileName, x.IsDirectory, x.Length, parent), options)
			{
				ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory,
			};

			var fileQuery = new FileSystemEnumerable<FileIndexModel>(Path, (ref FileSystemEntry x) => new FileIndexModel(x.FileName, x.IsDirectory, x.Length, parent), options)
			{
				ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory,
			};

			ThreadPool.QueueUserWorkItem(async x =>
			{
				var query = folderQuery.Concat(fileQuery);

				var comparer = new AsyncComparer<FileIndexModel>(async (x, y) =>
				{
					var resultX = await x.TaskSize;
					var resultY = await y.TaskSize;

					return resultY.CompareTo(resultX);
				});

				await view.Root.AddRange(query, comparer, token: CurrentTab.TokenSource.Token);
			});

			ThreadPool.QueueUserWorkItem(async x =>
			{
				var options = new EnumerationOptions()
				{
					IgnoreInaccessible = true,
					AttributesToSkip = FileAttributes.System,
					RecurseSubdirectories = true
				};

				await Dispatcher.UIThread.InvokeAsync(() => CurrentTab.IsLoading = true);

				var extensionQuery = new FileSystemEnumerable<(string Extension, long Size)>(Path, (ref FileSystemEntry x) => (System.IO.Path.GetExtension(x.FileName).ToString(), x.Length), options)
				{
					ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory
				}
					.Where(w => !String.IsNullOrEmpty(w.Extension))
					.GroupBy(g => g.Extension);

				var comparer = new ExtensionModelComparer();

				foreach (var extension in extensionQuery)
				{
					if (CurrentTab.TokenSource.IsCancellationRequested)
						break;

					if (!String.IsNullOrEmpty(extension.Key))
					{
						var model = new ExtensionModel(extension.Key, extension.Sum(s => s.Size))
						{
							TotalFiles = extension.Count()
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
		}

		public void ShowProperties()
		{
			if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null)
			{
				var model = CurrentTab.Files.FirstOrDefault(x => x.IsSelected) ?? new FileModel(Path, true);

				var properties = new Properties()
				{
					Model = model,
				};

				CurrentTab.PopupContent = properties;
			}
		}
	}
}