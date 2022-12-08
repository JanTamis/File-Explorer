using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Threading;
using DialogHostAvalonia;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using FileExplorer.Models;

namespace FileExplorer.Popup;

public sealed partial class Properties : UserControl, IPopup, INotifyPropertyChanged
{
	public new event PropertyChangedEventHandler PropertyChanged = delegate { };

	private string _path;

	private long _size = 0;

	private IFileItem _model;

	public bool HasShadow => false;
	public bool HasToBeCanceled => false;

	public string Title => "Properties";

	public event Action OnClose = delegate { };

	private CancellationTokenSource _source;

	public Task<IImage?> Icon => ThumbnailProvider.GetFileImage(Model, Provider, 48);

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
		init
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

				Task.Run(async () =>
				{
					_source = new CancellationTokenSource();

					var tempItems = Provider.GetItems(Model, "*", false, _source.Token)
						.Select(s =>
						{
							var result = Enumerable.Empty<IFileItem>();

							if (s.IsFolder)
							{
								result = Provider.GetItems(s, "*", true, _source.Token)
									.Where(w => !w.IsFolder);
							}

							return result.Prepend(s);
						});

					Task.WhenAll(tempItems.Select(s => Task.Factory.StartNew(_ =>
					{
						var timestamp = Stopwatch.GetTimestamp();
						var size = 0l;

						foreach (var item in s)
						{
							if (_source.IsCancellationRequested)
							{
								OnPropertyChanged(nameof(Size));
								break;
							}

							size += item.Size;

							if (Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds > 25 && size > 0)
							{
								Interlocked.Add(ref _size, size);
								size = 0;
								timestamp = Stopwatch.GetTimestamp();


							}
						}
					}, null, _source.Token, TaskCreationOptions.None, ObservableRangeCollection<IFileItem>.concurrentExclusiveScheduler.ConcurrentScheduler)));

					var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));

					while (await timer.WaitForNextTickAsync(_source.Token))
					{
						Dispatcher.UIThread.InvokeAsync(() => OnPropertyChanged(nameof(Size)));
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

	public IItemProvider Provider { get; set; }

	public Properties()
	{
		AvaloniaXamlLoader.Load(this);
		DataContext = this;
	}

	public void Close()
	{
		_source?.Cancel();
		DialogHost.Close(null);
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