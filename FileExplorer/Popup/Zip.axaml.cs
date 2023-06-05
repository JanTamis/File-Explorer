using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;
using DialogHostAvalonia;
using FileExplorer.Core.Interfaces;
using FileExplorer.Models;

namespace FileExplorer.Popup;

public sealed partial class Zip : UserControl, IPopup
{
  private IEnumerable<IFileItem> _selectedFiles;
  public new event PropertyChangedEventHandler? PropertyChanged = delegate { };

  public bool HasShadow => true;
  public bool HasToBeCanceled => true;
  public string Title => $"Zipping Items...";

  private CancellationTokenSource? _source;

  public event Action? OnClose;
  
  public Zip()
  {
    InitializeComponent();

    DataContext = this;
  }

  // public async Task ZipFiles()
  // {
  //   CurrentCount = 0;
  //   _source = new CancellationTokenSource();
  //
  //   var task = Task.Run(() =>
  //   {
  //     var path = String.Empty;
  //     var attempt = 0;
  //
  //     do
  //     {
  //       attempt++;
  //       path = TreeItem.GetPath(path => Path.Combine(path.ToString(), $"Archive{attempt}.zip"));
  //     } while (File.Exists(path));
  //
  //     using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
  //     {
  //       foreach (var file in SelectedFiles)
  //       {
  //         if (_source.IsCancellationRequested)
  //         {
  //           break;
  //         }
  //
  //         if (!file.IsFolder)
  //         {
  //           try
  //           {
  //             zip.CreateEntryFromFile(file.GetPath(path => path.ToString()), file.Name, CompressionLevel);
  //           }
  //           catch (Exception) { }
  //         }
  //
  //         CurrentCount++;
  //
  //         OnPropertyChanged(nameof(CurrentCount));
  //         OnPropertyChanged(nameof(Progress));
  //       }
  //     }
  //
  //     FileModel = new FileModel(new FileSystemTreeItem(Path.GetFileName(path), false, TreeItem));
  //
  //     CurrentCount = Count;
  //
  //     OnPropertyChanged(nameof(CurrentCount));
  //     OnPropertyChanged(nameof(Progress));
  //
  //     Close();
  //   }, _source.Token);
  //
  //   var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
  //
  //   while (await timer.WaitForNextTickAsync(_source.Token))
  //   {
  //     if (task.IsCompleted)
  //     {
  //       break;
  //     }
  //
  //     OnPropertyChanged(nameof(CurrentCount));
  //     OnPropertyChanged(nameof(Progress));
  //     OnPropertyChanged(nameof(CurrentFile));
  //   }
  // }

  public void Close()
  {
    _source?.Cancel();
    OnClose?.Invoke();

    if (DialogHost.IsDialogOpen(null))
    {
	    DialogHost.Close(null);
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