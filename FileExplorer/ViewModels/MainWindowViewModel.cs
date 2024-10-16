using System.IO;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using FileExplorer.Popup;
using System.Timers;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using FileExplorer.Core.Helpers;
using FileExplorer.Models;
using FileExplorer.Graph;
using FileExplorer.Providers;
using Avalonia.Controls.ApplicationLifetimes;
using Material.Icons;
using Avalonia.Controls;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using FileExplorer.Resources;

namespace FileExplorer.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
	public readonly WindowNotificationManager notificationManager;

	[ObservableProperty]
	private TabItemViewModel _currentTab;

	[ObservableProperty]
	private IEnumerable<string> _searchHistory;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SidebarWidth))]
	private bool _sidebarVisible = true;

	public int SidebarWidth => SidebarVisible ? 200 : 50;

	public static IEnumerable<SortEnum> SortValues => Enum.GetValues<SortEnum>();

	public IEnumerable<SideBarModel> Folders
	{
		get
		{
			yield return new SideBarModel(null, ResourceDefault.Dashboard, ThumbnailProvider.GetImage("Dashboard"));
			yield return new SideBarModel(Environment.SpecialFolder.Desktop, ResourceDefault.Desktop, ThumbnailProvider.GetImage("Desktop"));
			yield return new SideBarModel(Environment.SpecialFolder.MyDocuments, ResourceDefault.Documents, ThumbnailProvider.GetImage("MyDocuments"));
			yield return new SideBarModel(Environment.SpecialFolder.MyMusic, ResourceDefault.Music, ThumbnailProvider.GetImage("MyMusic"));
			yield return new SideBarModel(Environment.SpecialFolder.MyPictures, ResourceDefault.Pictures, ThumbnailProvider.GetImage("MyPictures"));
			yield return new SideBarModel(Environment.SpecialFolder.MyVideos, ResourceDefault.Videos, ThumbnailProvider.GetImage("MyVideos"));

			foreach (var drive in DriveInfo.GetDrives())
			{
				yield return new SideBarModel(drive.RootDirectory.FullName, drive.Name, null);
			}
		}
	}

	public SideBarModel? CurrentFolder
	{
		get => null;
		set => SetPath(FileSystemTreeItem.FromPath(value.Folder));
	}

	public ObservableRangeCollection<TabItemViewModel> Tabs { get; } = new();

	public MaterialIconKind ToggleIcon
	{
		get
		{
			if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: var window, })
			{
				return window?.WindowState switch
				{
					WindowState.Maximized => MaterialIconKind.WindowRestore,
					WindowState.Normal => MaterialIconKind.Maximize,
					_ => MaterialIconKind.Maximize
				};
			}

			return MaterialIconKind.Maximize;
		}
	}

	public bool IsWindows => OperatingSystem.IsWindows();
	public bool IsMacOS => OperatingSystem.IsMacOS();
	
	public Thickness WindowMargin { get; set; }

	public IEnumerable<ViewTypes> AllViewTypes
	{
		get
		{
			yield return ViewTypes.List;
			yield return ViewTypes.Grid;
			yield return ViewTypes.Gallery;
		}
	}

	public IEnumerable<SortEnum> AllSortTypes => Enum.GetValues<SortEnum>();

	public MainWindowViewModel(WindowNotificationManager manager)
	{
		notificationManager = manager;

		// var drives = from drive in DriveInfo.GetDrives()
		// 	where drive.IsReady
		// 	select new FolderModel(FileSystemTreeItem.FromPath(drive.RootDirectory.FullName), null, null);
		//
		// var quickAccess = from specialFolder in KnownFolders()
		// 	let path = Environment.GetFolderPath(specialFolder)
		// 	where !String.IsNullOrEmpty(path)
		// 	select new FolderModel(FileSystemTreeItem.FromPath(path), Enum.GetName(specialFolder).Humanize(), null);

		// Folders = quickAccess.Concat(drives);

		AddTab();

		if (IsMacOS)
		{
			WindowMargin = new Thickness(75, 0, 0, 0);
		}

		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window, })
		{
			Window.WindowStateProperty.Changed.Subscribe(delegate
			{
				OnPropertyChanged(nameof(ToggleIcon));

				if (IsMacOS)
				{
					if (window.WindowState is WindowState.Normal)
					{
						WindowMargin = new Thickness(75, 0, 0, 0);
					}
					else
					{
						WindowMargin = new Thickness(0, 0, 0, 0);
					}
					
					OnPropertyChanged(nameof(WindowMargin));
				}
			});
		}

		// return;
		//
		// IEnumerable<Environment.SpecialFolder> KnownFolders()
		// {
		// 	yield return Environment.SpecialFolder.Desktop;
		// 	yield return Environment.SpecialFolder.MyDocuments;
		// 	yield return Environment.SpecialFolder.MyMusic;
		// 	yield return Environment.SpecialFolder.MyPictures;
		// 	yield return Environment.SpecialFolder.MyVideos;
		// }
	}

	private void Timer_Elapsed(object sender, ElapsedEventArgs e)
	{
		StartSearch();
	}

	public void AddTab()
	{
		var tab = App.Container.Resolve<TabItemViewModel>().Value;

		Tabs.Add(tab);
		CurrentTab = tab;

		// tab.SetPath(new FileModel(FileSystemTreeItem.FromPath("/")));
	}

	public async Task GoUp()
	{
		if (CurrentTab.CurrentFolder is { IsRoot: false, })
		{
			CurrentTab.SetPath(await CurrentTab.Provider.GetParentAsync(CurrentTab.CurrentFolder, default));
		}
	}

	public async ValueTask StartSearch()
	{
		if (CurrentTab.Search is { Length: > 0, })
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

	public async ValueTask SetPath(FileSystemTreeItem? path)
	{
		if (path is not null)
		{
			if (CurrentTab.Provider is not FileSystemProvider)
			{
				CurrentTab.Provider = new FileSystemProvider();
			}

			await CurrentTab.SetPath(new FileModel(path));
		}
		else
		{
			CurrentTab.SetPath(null);
		}
	}

	public async void Refresh()
	{
		await CurrentTab.UpdateFiles(false, "*");
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

		// CurrentTab.OnPropertyChanged(nameof(CurrentTab.SelectionText));
		// CurrentTab.Files.PropertyChanged("IsSelected");
	}

	public void SelectInvert()
	{
		foreach (var file in CurrentTab.Files ?? Enumerable.Empty<IFileItem>())
		{
			file.IsSelected ^= true;
		}

		// CurrentTab.OnPropertyChanged(nameof(CurrentTab.SelectionText));
		// CurrentTab.Files.PropertyChanged("IsSelected");
	}

	public void ShowSettings()
	{
		if (CurrentTab.PopupContent is { HasToBeCanceled: false, } or null)
		{
			CurrentTab.PopupContent = new Settings();
		}
	}

	public void Rename()
	{
		if (CurrentTab.PopupContent is { HasToBeCanceled: false, } or null)
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
		// var data = new DataObject();
		// data.Set(DataFormats.FileNames, CurrentTab.Files.Where(x => x.IsSelected)
		// 	.Select(s => s.GetPath(path => path.ToString()))
		// 	.ToArray());
		//
		// await App.Current.Clipboard.SetDataObjectAsync(data);
		//
		// notificationManager.Show(new Notification("Copy Files", "Files has been copied"));
	}

	public async void CopyPath()
	{
		// await Application.Current.Clipboard.SetTextAsync(CurrentTab.CurrentFolder.GetPath(path => path.ToString()));
		//
		// notificationManager.Show(new Notification("Copy Path", "The path has been copied"));
	}

	public void CopyTo()
	{
		if (CurrentTab.PopupContent is { HasToBeCanceled: false, } or null && Tabs.Count(x => !String.IsNullOrEmpty(x.CurrentFolder.Name)) > 1)
		{
			var selector = new TabSelector
			{
				Tabs = new ObservableRangeCollection<TabItemViewModel>(Tabs.Where(x => x != CurrentTab && !String.IsNullOrEmpty(x.CurrentFolder.Name)))
			};

			selector.TabSelectionChanged += _ => { selector.Close(); };

			CurrentTab.PopupContent = selector;
		}
	}

	public void AnalyzeFolder()
	{

	}

	public void ShowProperties()
	{
		if (CurrentTab.PopupContent is { HasToBeCanceled: false, } or null)
		{
			var model = CurrentTab.Files.FirstOrDefault(x => x.IsSelected) ?? CurrentTab.CurrentFolder;

			var properties = new Properties
			{
				Provider = CurrentTab.Provider,
				Model = model
			};

			CurrentTab.PopupContent = properties;
		}
	}

	public async Task OneDrive()
	{
		var provider = new GraphItemProvider(async (code, url, token) =>
		{
			if (CurrentTab.PopupContent is { HasToBeCanceled: false, } or null)
			{
				await Dispatcher.UIThread.InvokeAsync(() =>
				{
					var login = new OneDriveLogin
					{
						Code = code,
						RedirectUri = url
					};

					CurrentTab.PopupContent = login;
				});
			}
		});

		CurrentTab.Provider = provider;

		await CurrentTab.SetPath(await provider.GetRootAsync());

		CurrentTab.PopupContent = null;
	}

	public void ToggleSidebar()
	{
		SidebarVisible = !SidebarVisible;
	}
}