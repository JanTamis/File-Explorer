using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using FileExplorer.Interfaces;
using FileExplorer.Models;

namespace FileExplorer.Popup;

public partial class Properties : UserControl, IPopup, INotifyPropertyChanged
{
	public new event PropertyChangedEventHandler PropertyChanged = delegate { };

	private string _path;

	private long _size = -1;

	private IFileItem _model;

	public bool HasShadow => false;
	public bool HasToBeCanceled => false;

	public string Title => "Properties";

	public event Action OnClose = delegate { };

	private CancellationTokenSource _source;

	public Task<IImage?> Icon => ThumbnailProvider.GetFileImage(_model, 48);

	private ObservableRangeCollection<MetadataExtractor.Directory> MetaData { get; } = new();

	public string CreatedOn
	{
		get
		{
			if (String.IsNullOrWhiteSpace(Path))
			{
				return String.Empty;
			}

			var date = File.Exists(Path) 
				? new FileInfo(Path).CreationTime 
				: new DirectoryInfo(Path).CreationTime;

			return $"{date.ToLongDateString()}, {date.ToLongTimeString()}";
		}
	}

	public IFileItem Model
	{
		get => _model;
		set
		{
			OnPropertyChanged(ref _model, value);
			Path = Model.GetPath(path => path.ToString());
			ItemName = _model.Name;
			_size = _model.Size;
			OnPropertyChanged(nameof(Size));
			OnPropertyChanged(nameof(ItemName));
				
			if (value.IsFolder)
			{
				_size = 0;
				OnPropertyChanged(nameof(Size));

				Task.Run(() =>
				{
					var enumerable = _model.GetPath(path => new FileSystemEnumerable<long>(path.ToString(), (ref FileSystemEntry x) => x.Length, new EnumerationOptions
					{
						RecurseSubdirectories = true,
						IgnoreInaccessible = true,
						AttributesToSkip = FileSystemTreeItem.Options.AttributesToSkip,
					}));

					var watch = Stopwatch.StartNew();

					_source = new CancellationTokenSource();
						
					foreach (var child in enumerable)
					{
						if (_source.IsCancellationRequested)
						{
							OnPropertyChanged(nameof(Size));
							break;
						}

						_size += child;

						if (watch.ElapsedMilliseconds > 50)
						{
							OnPropertyChanged(nameof(Size));

							watch.Restart();
						}
					}
				});	
			}
		}
	}

	public string Path
	{
		get => _path;
		set
		{
			OnPropertyChanged(ref _path, value);
			OnPropertyChanged(nameof(Icon));
			OnPropertyChanged(nameof(ItemName));
			OnPropertyChanged(nameof(Size));
			OnPropertyChanged(nameof(CreatedOn));

			MetaData.AddRange<Comparer<MetadataExtractor.Directory>>(MetaDataHelper.GetData(_path));
		}
	}

	public long Size => _size;

	public string ItemName { get; set; }

	public Properties()
	{
		AvaloniaXamlLoader.Load(this);
		DataContext = this;
	}

	public void Close()
	{
		_source?.Cancel();
		DialogHost.DialogHost.Close(null);
		OnClose();
	}

	public void Save()
	{
		// _model.Name = ItemName;
	}

	public void SaveAndQuit()
	{
		Save();
		Close();
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