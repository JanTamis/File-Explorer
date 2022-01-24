using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
using FileExplorerCore.Models;
using System.ComponentModel;
using System.IO;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FileExplorerCore.Popup
{
	public partial class Properties : UserControl, IPopup, INotifyPropertyChanged
	{
		public new event PropertyChangedEventHandler PropertyChanged = delegate { };

		private string path;

		private Bitmap? icon;

		private long size = -1;

		private FileModel model;

		public bool HasShadow => false;
		public bool HasToBeCanceled => false;

		public string Title => "Properties";

		public event Action OnClose = delegate { };

		public Bitmap? Icon => icon ??= OperatingSystem.IsWindows() 
			? WindowsThumbnailProvider.GetThumbnail(Path, 48, 48)
			: null;

		ObservableRangeCollection<MetadataExtractor.Directory> MetaData { get; set; } = new();

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
			get => model;
			set
			{
				model = value;
				Path = Model.Path;
				ItemName = model.Name;
				OnPropertyChanged(nameof(ItemName));
			}
		}


		public string Path
		{
			get => path;
			set
			{
				OnPropertyChanged(ref path, value);
				OnPropertyChanged(nameof(Icon));
				OnPropertyChanged(nameof(ItemName));
				OnPropertyChanged(nameof(Size));
				OnPropertyChanged(nameof(CreatedOn));

				MetaData.AddRange(MetaDataHelper.GetData(path));
			}
		}

		public long Size
		{
			get
			{
				if (size is -1 && !String.IsNullOrWhiteSpace(Path))
				{
					if (!model.IsFolder)
					{
						size = model.Size;
					}
					else if ((Path.EndsWith(System.IO.Path.DirectorySeparatorChar) || Path.EndsWith(System.IO.Path.AltDirectorySeparatorChar)) && new DriveInfo(Path[0].ToString()) is { IsReady: true } drive)
					{
						size = drive.TotalSize - drive.AvailableFreeSpace;
					}
					else if (Directory.Exists(Path))
					{
						var temp = new FileSystemEnumerable<long>(path, (ref FileSystemEntry x) => x.Length, new EnumerationOptions
						{
							IgnoreInaccessible = true,
							RecurseSubdirectories = true
						});

						size = 0;

						ThreadPool.QueueUserWorkItem(x =>
						{
							foreach (var item in temp)
							{
								size += item;
							}
						});

						ThreadPool.QueueUserWorkItem(async x =>
						{
							using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

							while (await timer.WaitForNextTickAsync())
							{
								OnPropertyChanged(nameof(Size));
							}
						});
					}
				}

				return size;
			}
		}

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
			OnClose();
		}

		public void Save()
		{
			model.Name = ItemName;
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

		protected void OnPropertyChanged<T>(T property, T value, [CallerMemberName] string name = null)
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
