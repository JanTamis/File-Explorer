using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using DialogHostAvalonia;
using FileExplorer.Core.Interfaces;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace FileExplorer.Popup;

public sealed partial class Zip : UserControl, IPopup
{
  private List<IFileItem> _selectedFiles;
  private int _progress;
  
  public List<IFileItem> SelectedFiles
  {
    get => _selectedFiles;
    set
    {
      OnPropertyChanged(ref _selectedFiles, value);
      OnPropertyChanged(nameof(FileCount));
    }
  }

  public IFileItem Folder
  {
    get => _folder;
    set => OnPropertyChanged(ref _folder, value);
  }

  public int Progress
  {
    get => _progress;
    set => OnPropertyChanged(ref _progress, value);
  }
  
  public int FileCount => SelectedFiles?.Count ?? 0;

  public new event PropertyChangedEventHandler? PropertyChanged = delegate { };

  public bool HasShadow => true;
  public bool HasToBeCanceled => false;
  public string Title => "Zipping Items...";

  private CancellationTokenSource? _source;
  private IFileItem _folder;

  public event Action? OnClose;

  public Zip()
  {
    InitializeComponent();
  }

  public async Task ZipFiles()
  {
    await Task.Run(() =>
    {
      using var archive = ArchiveFactory.Create(ArchiveType.Zip);

      foreach (var selectedFile in SelectedFiles)
      {
        _source?.Token.ThrowIfCancellationRequested();
        archive.AddEntry(selectedFile.Name, File.OpenRead(selectedFile.GetPath()), true);

        Progress++;
      }

      archive.SaveTo($"{Folder.GetPath()}/archive.zip", new WriterOptions(CompressionType.Deflate));
    });

    Close();
  }

  public void Close()
  {
    _source?.Cancel();
    OnClose?.Invoke();

    if (DialogHost.IsDialogOpen(null))
    {
	    DialogHost.Close(null);
    }
  }

  private void OnPropertyChanged<T>(ref T property, T value, [CallerMemberName] string name = null)
  {
    property = value;
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }

  public void OnPropertyChanged(string name)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}