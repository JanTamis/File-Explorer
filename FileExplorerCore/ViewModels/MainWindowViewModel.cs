using Avalonia.Controls.Notifications;
using Avalonia.Input;
using FileExplorerCore.Helpers;
using FileExplorerCore.Models;
using FileExplorerCore.Popup;
using Microsoft.VisualBasic.FileIO;
using Nessos.LinqOptimizer.CSharp;
using NetFabric.Hyperlinq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Timers;

namespace FileExplorerCore.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		private bool isSearching = false;
		public readonly WindowNotificationManager notificationManager;

		private TabItemViewModel _currentTab;
		private IEnumerable<string> searchHistory;

		public static IEnumerable<SortEnum> SortValues => Enum.GetValues<SortEnum>();

		public IEnumerable<FolderModel> Folders { get; set; }

		public ObservableRangeCollection<FileModel> Files => CurrentTab.Files;

		public ObservableRangeCollection<TabItemViewModel> Tabs { get; set; } = new();

		public IEnumerable<string> SearchHistory
		{
			get => searchHistory;
			set => this.RaiseAndSetIfChanged(ref searchHistory, value);
		}

		public TabItemViewModel CurrentTab
		{
			get => _currentTab;
			set
			{
				this.RaiseAndSetIfChanged(ref _currentTab, value);

				this.RaisePropertyChanged(nameof(Files));
				this.RaisePropertyChanged(nameof(Path));
			}
		}

		public string Path
		{
			get => CurrentTab.Path;
			set
			{
				if (value != Path)
				{
					CurrentTab.Path = value;

					this.RaisePropertyChanged();

					isSearching = false;

					CurrentTab.UpdateFiles(false, "*").ContinueWith(x =>
					{
						var categories = Enum.GetValues<Categories>().Select(s => s.ToString() + ":");
						SearchHistory = categories.Concat(CurrentTab.Files.Select(s => "*" + s.Extension).Distinct());
					});
				}
			}
		}

		public MainWindowViewModel(WindowNotificationManager manager)
		{
			notificationManager = manager;

			var drives = from drive in DriveInfo.GetDrives()
									 where drive.IsReady
									 select new FolderModel(drive.RootDirectory.FullName, $"{drive.VolumeLabel} ({drive.Name})");

			var quickAccess = from specialFolder in Enum.GetValues<KnownFolder>()
												select new FolderModel(KnownFolders.GetPath(specialFolder));

			Folders = quickAccess.Concat(drives);

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

		public async void StartSearch()
		{
			if (CurrentTab.Search is { Length: > 0 } && Path is { Length: > 0 })
			{
				isSearching = true;

				await CurrentTab.UpdateFiles(isSearching, CurrentTab.Search);
			}
		}

		public void Undo()
		{
			Path = CurrentTab.Undo();
		}

		public void Redo()
		{
			Path = CurrentTab.Redo();
		}

		public void CancelUpdateFiles()
		{
			CurrentTab.CancelUpdateFiles();
		}

		public void SetPath(string path)
		{
			if (CurrentTab is not null)
			{
				CurrentTab.SetPath(path);
			}
		}

		public async void Refresh()
		{
			if (!String.IsNullOrWhiteSpace(Path))
			{
				await CurrentTab.UpdateFiles(isSearching, isSearching ? CurrentTab.Search : "*");
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

				CurrentTab.RaisePropertyChanged(nameof(CurrentTab.SelectionText));
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

				CurrentTab.RaisePropertyChanged(nameof(CurrentTab.SelectionText));
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

				CurrentTab.RaisePropertyChanged(nameof(CurrentTab.SelectionText));
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
			if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null)
			{
				var selector = new TabSelector()
				{
					Tabs = Tabs.Where(x => x != CurrentTab),
				};
				CurrentTab.PopupContent = selector;
			}
		}

		public void RemoveTab()
		{
			//Tabs.Remove(tab);
		}
	}
}