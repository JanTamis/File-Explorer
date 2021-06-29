using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using FileExplorerCore.Helpers;
using FileExplorerCore.Models;
using FileExplorerCore.Popup;
using Microsoft.CodeAnalysis.CSharp;
using Nessos.LinqOptimizer.CSharp;
using NetFabric.Hyperlinq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace FileExplorerCore.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		private bool isSearching = false;

		WindowNotificationManager notificationManager;

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
			var tab = new TabItemViewModel(null);

			tab.PropertyChanged += Tab_PropertyChanged;

			Tabs.Add(tab);
			CurrentTab = tab;
		}

		private void Tab_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (sender is TabItemViewModel tab && tab == CurrentTab)
			{
				this.RaisePropertyChanged(e.PropertyName);
			}
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
			if (File.Exists(path))
			{
				try
				{
					var info = new ProcessStartInfo
					{
						FileName = path,
						UseShellExecute = true,
					};

					Process.Start(info);
				}
				catch (Exception) { }
			}
			else if (Directory.Exists(path))
			{
				Path = path;
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
			var fileIndex = CurrentTab.Files.IndexOf(CurrentTab.Files.FirstOrDefault(x => x.IsSelected));

			if (fileIndex is not -1 && CurrentTab.PopupContent is { HasToBeCanceled: false } or null)
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

		public async void CopyFiles()
		{
			var data = new DataObject();
			data.Set(DataFormats.FileNames, CurrentTab.Files.Where(x => x.IsSelected).Select(s => s.Path).ToArray());

			await App.Current.Clipboard.SetDataObjectAsync(data);

			notificationManager.Show(new Notification("Copy Files", "Files has been copied"));
		}

		public async void CopyPath()
		{
			await App.Current.Clipboard.SetTextAsync(CurrentTab.Path);

			notificationManager.Show(new Notification("Copy Path", "The path has been copied"));
		}

		public void RemoveTab()
		{
			//Tabs.Remove(tab);
		}
	}
}