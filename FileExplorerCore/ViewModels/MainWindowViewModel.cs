using Avalonia.Controls;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using FileExplorerCore.Helpers;
using FileExplorerCore.Models;
using FileExplorerCore.Popup;
using Nessos.LinqOptimizer.CSharp;
using NetFabric.Hyperlinq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Timers;

namespace FileExplorerCore.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		private bool isSearching = false;
		private bool isDarkMode;

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

		

		public MainWindowViewModel()
		{
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

				CurrentTab.Files.Refresh();
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
					file.IsSelected = !file.IsSelected;
				}
			}
		}

		public void ShowSettings()
		{
			CurrentTab.PopupContent = new Settings();
		}

		public void RemoveTab()
		{
			//Tabs.Remove(tab);
		}
	}
}