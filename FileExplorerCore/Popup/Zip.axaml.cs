using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FileExplorerCore.Interfaces;
using FileExplorerCore.Models;

namespace FileExplorerCore.Popup;

public partial class Zip : UserControl, IPopup, INotifyPropertyChanged
{
  private IEnumerable<FileModel> _selectedFiles;
  public new event PropertyChangedEventHandler? PropertyChanged = delegate { };

  public bool HasShadow => true;
  public bool HasToBeCanceled => true;
  public string Title => $"Zipping Items...";

  private CancellationTokenSource? _source;

  public event Action? OnClose;

  public IEnumerable<FileModel> SelectedFiles
  {
    get => _selectedFiles;
    set
    {
      _selectedFiles = value;

      OnPropertyChanged(nameof(CurrentCount));
      OnPropertyChanged(nameof(Count));
    }
  }

  public FileSystemTreeItem TreeItem { get; set; }
  public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
  public int Count => SelectedFiles?.Count() ?? 1;
  public int CurrentCount { get; set; }

  public string Progress => (CurrentCount / (double)Count).ToString("P");

  public FileModel? FileModel { get; private set; }

  public string CurrentFile { get; set; }

  public Zip()
  {
    AvaloniaXamlLoader.Load(this);

    DataContext = this;
  }

  public async Task ZipFiles()
  {
    CurrentCount = 0;
    _source = new CancellationTokenSource();

    var task = Task.Run(() =>
    {
      var path = String.Empty;
      var attempt = 0;

      do
      {
        attempt++;
        path = TreeItem.GetPath(path => Path.Combine(path.ToString(), $"Archive{attempt}.zip"));
      } while (File.Exists(path));

      using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
      {
        foreach (var file in SelectedFiles)
        {
          if (_source.IsCancellationRequested)
          {
            break;
          }

          CurrentFile = file.Name;

          if (!file.IsFolder)
          {
            try
            {
              zip.CreateEntryFromFile(file.TreeItem.GetPath(path => path.ToString()), file.Name, CompressionLevel);
            }
            catch (Exception) { }
          }

          CurrentCount++;

          OnPropertyChanged(nameof(CurrentCount));
          OnPropertyChanged(nameof(Progress));
        }
      }

      FileModel = new FileModel(new FileSystemTreeItem(Path.GetFileName(path), false, TreeItem));

      CurrentCount = Count;

      OnPropertyChanged(nameof(CurrentCount));
      OnPropertyChanged(nameof(Progress));

      Close();
    }, _source.Token);

    var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

    while (await timer.WaitForNextTickAsync(_source.Token))
    {
      if (task.IsCompleted)
      {
        break;
      }

      OnPropertyChanged(nameof(CurrentCount));
      OnPropertyChanged(nameof(Progress));
      OnPropertyChanged(nameof(CurrentFile));
    }
  }

  public void Close()
  {
    _source?.Cancel();
    OnClose?.Invoke();

    if (DialogHost.DialogHost.IsDialogOpen(null))
    {
      DialogHost.DialogHost.Close(null);
    }
  }

  protected void OnPropertyChanged<T>(ref T property, T value, [CallerMemberName] string name = null)
  {
    property = value;
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }

  public void OnPropertyChanged(string name)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}