using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
using FileExplorerCore.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerCore.Popup
{
	public partial class Properties : UserControl, IPopup, INotifyPropertyChanged
	{
		public new event PropertyChangedEventHandler PropertyChanged = delegate { };

		private string _path;

		private Bitmap? _icon;

		private long _size = -1;

		private FileModel _model;

		public bool HasShadow => false;
		public bool HasToBeCanceled => false;

		public string Title => "Properties";

		public event Action OnClose = delegate { };

		private CancellationTokenSource _source;

		public Bitmap? Icon => _icon ??= OperatingSystem.IsWindows() 
			? WindowsThumbnailProvider.GetThumbnail(Path, 48, 48)
			: null;

		private ObservableRangeCollection<MetadataExtractor.Directory> MetaData { get; } = new();

		public string CreatedOn
		{
			get
			{
				if (String.IsNullOrWhiteSpace(Path))
				{
					return String.Empty;
				}

				var date = File.Exists(Path) ? new FileInfo(Path).CreationTime : new DirectoryInfo(Path).CreationTime;

				return $"{date.ToLongDateString()}, {date.ToLongTimeString()}";
			}
		}

		public FileModel Model
		{
			get => _model;
			set
			{
				_model = value;
				Path = Model.Path;
				ItemName = _model.Name;
				_size = _model.Size;
				
				OnPropertyChanged(nameof(ItemName));
				OnPropertyChanged(nameof(Size));

				ThreadPool.QueueUserWorkItem(_ =>
				{
					var enumerable = _model.TreeItem.GetPath(path => new FileSystemEnumerable<long>(path.ToString(), (ref FileSystemEntry x) => x.Length, new EnumerationOptions()
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
			InitializeComponent();
			DataContext = this;

			void InitializeComponent()
			{
				AvaloniaXamlLoader.Load(this);
			}
		}

		public void Close()
		{
			_source?.Cancel();
			OnClose();
		}

		public void Save()
		{
			_model.Name = ItemName;
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
}
