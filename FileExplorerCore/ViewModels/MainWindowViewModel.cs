using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Threading;
using DialogHost;
using FileExplorerCore.DisplayViews;
using FileExplorerCore.Helpers;
using FileExplorerCore.Injection;
using FileExplorerCore.Models;
using FileExplorerCore.Popup;
using Microsoft.Toolkit.HighPerformance;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace FileExplorerCore.ViewModels;

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
      foreach (var tab in Tabs)
      {
        tab.IsSelected = tab == value;
      }

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
        .OrderBy(o => o.DriveType)
        .ThenBy(t => t.Name)
        .Select(s =>
        {
          var treeItem = new FileSystemTreeItem(s.Name, true);
          return new FolderModel(treeItem, $"{s.VolumeLabel} ({s.Name})", null);
        });

      var quickAccess = from specialFolder in Enum.GetValues<KnownFolder>()
                        select new FolderModel(GetTreeItem(KnownFolders.GetPath(specialFolder)));

      var result = new[]
      {
        new FolderModel("Quick Access", quickAccess),
        new FolderModel("Drives", drives),
      };

      Folders = new ObservableRangeCollection<FolderModel>(result, true);
    }
    else
    {
      Folders = new ObservableRangeCollection<FolderModel>(new[]
      {
        new FolderModel(new FileSystemTreeItem(new string(PathHelper.DirectorySeparator, 1), true))
      });
    }

    _currentTab = null!;

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

    tab.PathChanged += async () => await OnPropertyChanged(nameof(SearchHistory));
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

  public async Task ShowSettings()
  {
    if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null)
    {
      await App.Container.Run<Settings, Task>(setting => DialogHost.DialogHost.Show(setting));

      CurrentTab.PopupContent = null;
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

  public async Task ZipFiles()
  {
    if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null && CurrentTab.TreeItem is not null)
    {
      var zip = new Zip
      {
        SelectedFiles = Files.Where(w => w.IsSelected),
        TreeItem = CurrentTab.TreeItem,
        CompressionLevel = CompressionLevel.SmallestSize,
      };
      zip.ZipFiles();

      await DialogHost.DialogHost.Show(zip, (object sender, DialogClosingEventArgs eventArgs) =>
      {
        zip.Close();
      });

      CurrentTab.PopupContent = null;

      if (zip.FileModel is not null)
      {
        CurrentTab.Files.Add(zip.FileModel);
      }
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

  public async Task DeleteFiles()
  {
    var selectedFiles = CurrentTab.Files.Where(x => x.IsSelected);
    var selectedFileCount = CurrentTab.SelectionCount;

    if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null && selectedFileCount > 0)
    {
      var choice = new Choice
      {
        CloseText = "Cancel",
        SubmitText = "Delete",
        Message = selectedFileCount is 1
          ? $"Are you sure you want to delete {selectedFiles.First().Name}?"
          : $"Are you sure you want to delete {selectedFileCount} items?",
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

        DialogHost.DialogHost.Close(null);
      };

      CurrentTab.PopupContent = choice;

      await DialogHost.DialogHost.Show(choice);
      CurrentTab.PopupContent = null;
    }
  }

  public void CopyTo()
  {
    if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null && Tabs.Count(x => x.TreeItem is not null && !String.IsNullOrWhiteSpace(x.TreeItem.GetPath(x => x.ToString()))) > 1)
    {
      var selector = new TabSelector
      {
        Tabs = new ObservableRangeCollection<TabItemViewModel>(Tabs.Where(x => x.TreeItem is not null && x != CurrentTab && !String.IsNullOrWhiteSpace(x.TreeItem.GetPath(x => x.ToString())))),
      };

      selector.TabSelectionChanged += _ => { selector.Close(); };

      CurrentTab.PopupContent = selector;
    }
  }

  public async Task AnalyzeFolder()
  {
    if (CurrentTab.TreeItem is null)
    {
      return;
    }

    var view = new AnalyzerView();
    CurrentTab.DisplayControl = view;

    CurrentTab.TokenSource?.Cancel();
    CurrentTab.TokenSource = new System.Threading.CancellationTokenSource();

    var rootTask = Task.Run(async () =>
    {
      var query = CurrentTab.TreeItem
        .EnumerateChildren()
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

  public async Task ShowProperties()
  {
    if (CurrentTab.PopupContent is { HasToBeCanceled: false } or null)
    {
      var model = CurrentTab.Files.FirstOrDefault(x => x.IsSelected);

      if (model is not null)
      {
        await App.Container.Run<Properties, Task, FileModel>(async (properties, model) =>
        {
          properties.Model = model;

          await DialogHost.DialogHost.Show(properties);

          properties.Close();
        }, model);

        CurrentTab.PopupContent = null;
      }
    }
  }

  public static FileSystemTreeItem GetTreeItem(ReadOnlySpan<char> path)
  {
    var offset = OperatingSystem.IsWindows()
      ? 3
      : 1;

    var item = new FileSystemTreeItem(path[..offset], true);

    foreach (var name in path[offset..].Tokenize(PathHelper.DirectorySeparator))
    {
      item = new FileSystemTreeItem(name, true, item);
    }

    return item;
  }
}