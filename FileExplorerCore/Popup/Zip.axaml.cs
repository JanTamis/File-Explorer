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

	public Zip()
	{
		AvaloniaXamlLoader.Load(this);

		DataContext = this;
	}

	public async Task ZipFiles()
	{
		var count = 0;
		_source = new CancellationTokenSource();
		
		var task = Task.Run(() =>
		{
			var path = TreeItem.GetPath(path => Path.Combine(path.ToString(), "Archive1.zip"));

			using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
			{
				foreach (var file in SelectedFiles)
				{
					if (_source.IsCancellationRequested)
					{
						break;
					}
					
					try
					{
						if (!file.IsFolder)
						{
							zip.CreateEntryFromFile(file.TreeItem.GetPath(path => path.ToString()), file.Name, CompressionLevel);
						}
					}
					finally
					{
						count++;
					}
				}
			}

			FileModel = new FileModel(new FileSystemTreeItem("Archive1.zip", false, TreeItem));

			Close();
		}, _source.Token);

		var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

		while (await timer.WaitForNextTickAsync(_source.Token))
		{
			CurrentCount = count;
			OnPropertyChanged(nameof(CurrentCount));
			OnPropertyChanged(nameof(Progress));

			if (task.IsCompleted)
			{
				break;
			}
		}

		if (_source.IsCancellationRequested)
		{
			var path = TreeItem.GetPath(path => Path.Combine(path.ToString(), "Archive1.zip"));
			File.Delete(path);
		}
	}

	public void Close()
	{
		_source?.Cancel();
		OnClose?.Invoke();
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