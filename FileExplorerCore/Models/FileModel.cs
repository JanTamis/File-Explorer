using Avalonia.Media;
using FileExplorerCore.Helpers;
using Humanizer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using Avalonia.Threading;
using System.Runtime.CompilerServices;

namespace FileExplorerCore.Models;

public class FileModel : INotifyPropertyChanged
{
  public static readonly ConcurrentBag<FileModel> FileImageQueue = new();

  public event PropertyChangedEventHandler PropertyChanged = delegate { };

  public FileSystemTreeItem TreeItem { get; }

  private bool _isSelected;
  private string? _name;
  private string? _extension;
  private long _size = -1;

  private DateTime _editedOn;

  private bool isVisible = true;

  public string? ExtensionName { get; set; }

  public bool IsVisible
  {
    get => isVisible;
    set
    {
      OnPropertyChanged(ref isVisible, value);

      if (IsVisible)
      {
        OnPropertyChanged(nameof(Image));
      }
    }
  }

  public Task<IImage?> Image
  {
    get
    {
      return ThumbnailProvider.GetFileImage(TreeItem, App.MainViewModel?.CurrentTab.CurrentViewMode is ViewTypes.Grid ? 100 : 24, () => IsVisible);
    }
  }

  public bool IsSelected
  {
    get => _isSelected;
    set
    {
      if (_isSelected != value)
      {
        OnPropertyChanged(ref _isSelected, value);
      }
    }
  }

  public IEnumerable<FileModel> Children => TreeItem.Children.Select(s => new FileModel(s));

  public string Path => TreeItem.GetPath(path => path.ToString());

  public string Name
  {
    get => TreeItem.IsFolder
      ? TreeItem.Value
      : TreeItem.GetPath(path => System.IO.Path.GetFileNameWithoutExtension(path).ToString());
    set
    {
      TreeItem.Value = value;

      try
      {
        var path = Path;

        if (!IsFolder)
        {
          var name = System.IO.Path.GetFileNameWithoutExtension(path);
          var extension = Extension;
          var newPath = path.Replace(name + extension, value + extension);

          File.Move(path, newPath);
        }
        else if (Directory.Exists(path))
        {
          var name = System.IO.Path.GetFileNameWithoutExtension(path);
          var newPath = path.Replace(name, value);

          Directory.Move(path, newPath);
        }
      }
      catch (Exception)
      {
        // ignored
      }

      OnPropertyChanged(ref _name, value);
    }
  }

  public string Extension => _extension ??= !IsFolder
    ? TreeItem.GetPath(path => System.IO.Path.GetExtension(path).ToString())
    : String.Empty;

  public long Size
  {
    get
    {
      if (_size == -1 && !IsFolder && new FileInfo(Path) is { Exists: true } info)
      {
        _size = info.Length;
      }

      return _size;
    }
  }

  public long TotalSize => IsFolder
    ? TreeItem.GetPath(path => new FileSystemEnumerable<long>(path.ToString(), (ref FileSystemEntry x) => x.Length, new EnumerationOptions
    {
      RecurseSubdirectories = true,
      IgnoreInaccessible = true,
      AttributesToSkip = FileSystemTreeItem.Options.AttributesToSkip,
    })).Sum()
    : Size;

  public bool IsFolder => TreeItem.IsFolder;

  public Task<string> SizeFromTask => Task.Run(() =>
  {
    var size = TreeItem.GetPath((path, state) =>
    {
      var result = 0L;

      if (!state.IsFolder)
      {
        result = state.Size;
      }
      else if (path[^1] == PathHelper.DirectorySeparator && new DriveInfo(new String(path[0], 1)) is { IsReady: true } info)
      {
        result = info.TotalSize - info.TotalFreeSpace;
      }
      else if (state.IsFolder)
      {
        var query = new FileSystemEnumerable<long>(path.ToString(), (ref FileSystemEntry x) => x.Length,
        new EnumerationOptions { RecurseSubdirectories = true })
        {
          ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory,
        };

        result = query.Sum();
      }

      return result;
    }, this);

    return size.Bytes().ToString();
  });

  public DateTime EditedOn
  {
    get
    {
      if (_editedOn == default)
      {
        if (!IsFolder)
        {
          _editedOn = File.GetLastWriteTime(Path);
        }
        else
        {
          _editedOn = Directory.GetLastWriteTime(Path);
        }
      }

      return _editedOn;
    }
  }

  public FileModel(FileSystemTreeItem item)
  {
    TreeItem = item;
  }

  public void OnPropertyChanged([CallerMemberName] string? name = null)
  {
    if (Dispatcher.UIThread.CheckAccess())
    {
      PropertyChanged(this, new PropertyChangedEventArgs(name));
    }
    else
    {
      Dispatcher.UIThread.InvokeAsync(() => PropertyChanged(this, new PropertyChangedEventArgs(name)));
    }
  }

  public void OnPropertyChanged<T>(ref T field, T value, [CallerMemberName] string? name = null)
  {
    field = value;
    OnPropertyChanged(name);
  }
}