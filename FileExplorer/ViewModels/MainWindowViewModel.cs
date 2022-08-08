using System;
using System.Collections.Generic;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Threading;
using FileExplorer.Helpers;
using FileExplorer.Popup;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using FileExplorer.Core.Interfaces;
using FileExplorer.DisplayViews;
using FileExplorer.Models;
using FileExplorer.Providers;

namespace FileExplorer.ViewModels
{
	[INotifyPropertyChanged]
	public partial class MainWindowViewModel
	{
		public readonly WindowNotificationManager notificationManager;

		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(Path))]
		private TabItemViewModel _currentTab;

		[ObservableProperty] private IEnumerable<string> _searchHistory;

		public static IEnumerable<SortEnum> SortValues => Enum.GetValues<SortEnum>();

		public IEnumerable<IPathSegment> Folders { get; set; }

		public IPathSegment CurrentFolder
		{
			get => Folders.FirstOrDefault();
			set
			{
				if (value is FolderModel { TreeItem: var treeItem })
				{
					SetPath(treeItem);
				}
			}
		}

		public ObservableRangeCollection<TabItemViewModel> Tabs { get; } = new();


		public string Path
		{
			get => CurrentTab.Path;
			set
			{
				if (value == Path)
				{
					return;
				}

				CurrentTab.SetPath(value);

				this.OnPropertyChanged();

				CurrentTab.UpdateFiles(false, "*").ContinueWith(x =>
				{
					var categories = Enum.GetValues<Categories>().Select(s => s + ":");
					SearchHistory = categories.Concat(CurrentTab.Files.Select(s => "*" + s.Extension).Distinct());
				});
			}
		}

		public MainWindowViewModel(WindowNotificationManager manager)
		{
			notificationManager = manager;

			var drives = from drive in DriveInfo.GetDrives()
				where drive.IsReady
				select new FolderModel(PathHelper.FromPath(drive.RootDirectory.FullName), String.Empty, null);

			// var quickAccess = from specialFolder in Enum.GetValues<KnownFolder>()
			// 	select new FolderModel(PathHelper.FromPath(KnownFolders.GetPath(specialFolder).ToString()));

			Folders = drives;

			AddTab();
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
			CurrentTab.SetPath(CurrentTab.Undo());

			await CurrentTab.UpdateFiles(false, "*");
		}

		public async ValueTask Redo()
		{
			CurrentTab.SetPath(CurrentTab.Redo());
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
				if (CurrentTab.Provider is not FileSystemProvider)
				{
					CurrentTab.Provider = new FileSystemProvider();
				}

				await CurrentTab.SetPath(path.GetPath(path => path.ToString()));
			}
		}

		public async void Refresh()
		{
			if (!String.IsNullOrWhiteSpace(Path))
			{
				await CurrentTab.UpdateFiles(CurrentTab.IsSearching, CurrentTab.IsSearching ? CurrentTab.Search : "*");
			}
		}

		public void SelectAll()
		{
			foreach (var file in CurrentTab.Files)
			{
				file.IsSelected = true;
			}

			// CurrentTab.OnPropertyChanged(nameof(CurrentTab.SelectionText));
			// CurrentTab.Files.PropertyChanged("IsSelected");
		}

		public void SelectNone()
		{
			foreach (var file in CurrentTab.Files)
			{
				file.IsSelected = false;
			}

			//CurrentTab.OnPropertyChanged(nameof(CurrentTab.SelectionText));
			// CurrentTab.Files.PropertyChanged("IsSelected");
		}

		public void SelectInvert()
		{
			foreach (var file in CurrentTab.Files)
			{
				file.IsSelected ^= true;
			}

			// CurrentTab.OnPropertyChanged(nameof(CurrentTab.SelectionText));
			// CurrentTab.Files.PropertyChanged("IsSelected");
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
				// var fileIndex = CurrentTab.Files.IndexOf(CurrentTab.Files.FirstOrDefault(x => x.IsSelected));
				//
				// if (fileIndex is not -1)
				// {
				// 	var rename = new Rename
				// 	{
				// 		// Files = CurrentTab.Files,
				// 		Index = fileIndex,
				// 	};
				//
				// 	CurrentTab.PopupContent = rename;
				//
				// 	rename.OnPropertyChanged(nameof(rename.File));
				// }
			}
		}

		public async void CopyFiles()
		{
			var data = new DataObject();
			data.Set(DataFormats.FileNames, CurrentTab.Files.Where(x => x.IsSelected)
				.Select(s => s.GetPath(path => path.ToString()))
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
					var deletedFiles = new List<IFileItem>(SelectedFileCount);

					foreach (var file in selectedFiles)
					{
						var path = file.GetPath(path => path.ToString());

						try
						{
							if (File.Exists(path))
							{
								FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
							}
							else if (Directory.Exists(path))
							{
								FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
							}

							deletedFiles.Add(file);
						}
						catch (Exception)
						{
						}
					}

					CurrentTab.PopupContent = null;
				};

				CurrentTab.PopupContent = choice;
			}
		}

		public void CopyTo()
		{
			if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null && Tabs.Count(x => !String.IsNullOrEmpty(x.Path)) > 1)
			{
				var selector = new TabSelector()
				{
					Tabs = new ObservableRangeCollection<TabItemViewModel>(Tabs.Where(x => x != CurrentTab && !String.IsNullOrEmpty(x.Path))),
				};

				selector.TabSelectionChanged += _ => { selector.Close(); };

				CurrentTab.PopupContent = selector;
			}
		}

		public void AnalyzeFolder()
		{
			var view = new AnalyzerView();
			CurrentTab.DisplayControl = view;

			var drive = new DriveInfo(Path.Substring(0, 1));
			var parent = new FileIndexModel(PathHelper.FromPath(Path));

			if (drive.Name == Path)
			{
				parent = new FileIndexModel(PathHelper.FromPath(Path));
			}

			var options = new EnumerationOptions()
			{
				IgnoreInaccessible = true,
				AttributesToSkip = FileAttributes.Temporary,
			};

			var folderQuery = new FileSystemEnumerable<FileIndexModel>(Path, (ref FileSystemEntry x) => new FileIndexModel(new FileSystemTreeItem(x.FileName, x.IsDirectory)), options)
			{
				ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory,
			};

			var fileQuery = new FileSystemEnumerable<FileIndexModel>(Path, (ref FileSystemEntry x) => new FileIndexModel(new FileSystemTreeItem(x.FileName, x.IsDirectory)), options)
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

				await view.Root.AddRangeAsync(query, comparer, token: CurrentTab.TokenSource.Token);
			});

			ThreadPool.QueueUserWorkItem(async x =>
			{
				var options = new EnumerationOptions()
				{
					IgnoreInaccessible = true,
					AttributesToSkip = FileAttributes.System,
					RecurseSubdirectories = true,
				};

				await Dispatcher.UIThread.InvokeAsync(() => CurrentTab.IsLoading = true);

				var extensionQuery = new FileSystemEnumerable<(string Extension, long Size)>(Path, (ref FileSystemEntry y) => (System.IO.Path.GetExtension(y.FileName).ToString(), y.Length), options)
					{
						ShouldIncludePredicate = (ref FileSystemEntry z) => !z.IsDirectory,
					}
					.Where(w => !String.IsNullOrEmpty(w.Extension))
					.GroupBy(g => g.Extension)
					.Where(w => CurrentTab.TokenSource?.IsCancellationRequested != true);

				var comparer = new ExtensionModelComparer();

				foreach (var extension in extensionQuery)
				{
					var model = new ExtensionModel(extension.Key, extension.Sum(s => s.Size))
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

				await Dispatcher.UIThread.InvokeAsync(() => CurrentTab.IsLoading = false);
			});
		}

		public void ShowProperties()
		{
			if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null)
			{
				var model = CurrentTab.Files.FirstOrDefault(x => x.IsSelected) ?? new FileModel(PathHelper.FromPath(Path));

				var properties = new Properties
				{
					Model = model,
				};

				CurrentTab.PopupContent = properties;
			}
		}
	}
}