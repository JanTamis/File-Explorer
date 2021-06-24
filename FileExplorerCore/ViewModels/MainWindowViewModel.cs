using Avalonia.Controls;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using FileExplorerCore.Helpers;
using FileExplorerCore.Models;
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
		private bool settingsVisible;
		private bool isDarkMode;

		private TabItemViewModel _currentTab;
		private IEnumerable<string> searchHistory;

		public static IEnumerable<SortEnum> SortValues => Enum.GetValues<SortEnum>();

		public SortEnum Sort
		{
			get => CurrentTab.Sort;
			set
			{
				CurrentTab.Sort = value;

				this.RaisePropertyChanged();
			}
		}

		public int Count
		{
			get => CurrentTab.Count;
			set
			{
				CurrentTab.Count = value;

				this.RaisePropertyChanged();
				this.RaisePropertyChanged(nameof(SearchProgression));

				if (!IsIndeterminate)
				{
					this.RaisePropertyChanged(nameof(SearchText));
				}
			}
		}

		public int FileCount => CurrentTab.FileCount;

		public double SearchProgression => CurrentTab.SearchProgression;

		public string SearchText => CurrentTab.SearchText;

		public bool IsIndeterminate => CurrentTab.IsIndeterminate;

		public bool IsLoading => CurrentTab.IsLoading;

		public IEnumerable<FolderModel> Folders { get; set; }

		public TimeSpan LoadTime => CurrentTab.LoadTime;

		public bool SearchFailed => CurrentTab.SearchFailed;

		public Control DisplayControl => CurrentTab.DisplayControl;

		public bool IsGrid
		{
			get => CurrentTab.IsGrid;
			set => CurrentTab.IsGrid = value;
		}

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

				this.RaisePropertyChanged(nameof(Sort));
				this.RaisePropertyChanged(nameof(Count));
				this.RaisePropertyChanged(nameof(FileCount));
				this.RaisePropertyChanged(nameof(LoadTime));
				this.RaisePropertyChanged(nameof(IsLoading));
				this.RaisePropertyChanged(nameof(IsIndeterminate));
				this.RaisePropertyChanged(nameof(SearchProgression));
				this.RaisePropertyChanged(nameof(SearchText));
				this.RaisePropertyChanged(nameof(Files));
				this.RaisePropertyChanged(nameof(Path));
				this.RaisePropertyChanged(nameof(Search));
				this.RaisePropertyChanged(nameof(SearchFailed));
				this.RaisePropertyChanged(nameof(DisplayControl));
				this.RaisePropertyChanged(nameof(IsGrid));
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

		public string? Search
		{
			get => CurrentTab.Search;
			set
			{
				CurrentTab.Search = value;

				this.RaisePropertyChanged();
			}
		}

		public bool SettingsVisible
		{
			get => settingsVisible;
			set => this.RaiseAndSetIfChanged(ref settingsVisible, value);
		}

		public bool IsDarkMode
		{
			get => isDarkMode;
			set
			{
				this.RaiseAndSetIfChanged(ref isDarkMode, value);

				ThreadPool.QueueUserWorkItem(x =>
				{
					var fluentTheme = new FluentTheme(new Uri(@"avares://FileExplorer"))
					{
						Mode = IsDarkMode ? FluentThemeMode.Dark : FluentThemeMode.Light,
					};

					Dispatcher.UIThread.Post(() => App.Current.Styles[0] = fluentTheme);
				});
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
			if (Search is { Length: > 0 } && Path is { Length: > 0 })
			{
				isSearching = true;

				await CurrentTab.UpdateFiles(isSearching, Search);
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
				await CurrentTab.UpdateFiles(isSearching, isSearching ? Search : "*");
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

				Files.Refresh();
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

		public void CloseSettings()
		{
			SettingsVisible = false;
		}

		public void ShowSettings()
		{
			SettingsVisible = true;
		}

		public void RemoveTab()
		{
			//Tabs.Remove(tab);
		}
	}
}