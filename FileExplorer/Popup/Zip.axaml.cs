using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using DialogHostAvalonia;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;
using SharpCompress.Writers.Tar;
using SharpCompress.Writers.Zip;
using CompressionLevel = SharpCompress.Compressors.Deflate.CompressionLevel;

namespace FileExplorer.Popup;

public sealed partial class Zip : UserControl, IPopup, INotifyPropertyChanged
{
	private List<IFileItem> _selectedFiles;
	private long _progress;
	private string _fileName;
	private bool _isRunning;

	private EtaCalculator _etaCalculator;

	private DateTime _startTime;

	public List<IFileItem> SelectedFiles
	{
		get => _selectedFiles;
		set
		{
			OnPropertyChanged(ref _selectedFiles, value);

			TotalSize = SelectedFiles.Sum(s =>
			{
				using var handle = File.OpenHandle(s.GetPath());

				return RandomAccess.GetLength(handle);
			});
			
			OnPropertyChanged(nameof(TotalSize));
		}
	}

	public IFileItem Folder
	{
		get => _folder;
		set => OnPropertyChanged(ref _folder, value);
	}

	public long Progress
	{
		get => _progress;
		set => OnPropertyChanged(ref _progress, value);
	}

	public string FileName
	{
		get => _fileName;
		set => OnPropertyChanged(ref _fileName, value);
	}

	public bool IsRunning
	{
		get => _isRunning;
		set => OnPropertyChanged(ref _isRunning, value);
	}

	public TimeSpan EstimatedTime => _etaCalculator?.ETR ?? TimeSpan.Zero;

	public long TotalSize { get; private set; }

	public new event PropertyChangedEventHandler? PropertyChanged = delegate { };

	public bool HasShadow => true;
	public bool HasToBeCanceled => true;
	public string Title => "Zipping Items...";

	private IFileItem _folder;

	public event Action? OnClose;

	public Zip()
	{
		InitializeComponent();
	}

	public async Task ZipFiles()
	{
		IsRunning = true;

		_startTime = DateTime.Now;
		_etaCalculator = new EtaCalculator(0, 30);

		await Task.Run(() =>
		{
			using var destinationStream = File.Create(Path.Combine(Folder.GetPath(), FileName));
			var extension = Path.GetExtension(FileName.AsSpan());

			IWriter? writer = null;

			switch (extension)
			{
				case ".zip":
				case ".zipx":
				case ".cbz":
					writer = new ZipWriter(destinationStream, new ZipWriterOptions(CompressionType.Deflate)
					{
						DeflateCompressionLevel = CompressionLevel.Default,
						UseZip64 = true
					});
					break;
					case ".tar":
					case ".taz":
					case ".tgz":
					case ".tb2":
					case ".tbz":
					case ".tbz2":
					case ".tz2":
					case ".tZ":
					case ".taZ":
						writer = new TarWriter(destinationStream, new TarWriterOptions(CompressionType.GZip, true));
						break;
					case ".gz":
						writer = new GZipWriter(destinationStream, new GZipWriterOptions());
						break;
			}

			if (writer is not null)
			{
				ZipFiles(writer);
			}
		});
		
		Close();
	}

	public void Close()
	{
		OnClose?.Invoke();

		if (DialogHost.IsDialogOpen(null))
		{
			DialogHost.Close(null);
		}

		IsRunning = false;
	}

	private void ZipFiles(IWriter writer)
	{
		Task.Run(async () =>
		{
			using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

			while (IsRunning && await timer.WaitForNextTickAsync())
			{
				OnPropertyChanged(nameof(EstimatedTime));
			}
		});
		
		foreach (var selectedFile in SelectedFiles)
		{
			if (!IsRunning)
			{
				break;
			}
			
			var path = selectedFile.GetPath();

			if (File.Exists(path))
			{
				using var file = File.OpenRead(path);

				Task.Run(async () =>
				{
					using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(25));

					var previous = 0L;

					while (IsRunning && await timer.WaitForNextTickAsync())
					{
						Progress += file.Position - previous;
						
						_etaCalculator.Update((double)((decimal)Progress / (decimal)TotalSize));
						
						previous = file.Position;
					}
				});

				writer.Write(selectedFile.Name, file);
			}
		}
	}

	private void OnPropertyChanged<T>(ref T property, T value, [CallerMemberName] string? name = null)
	{
		property = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	private void OnPropertyChanged(string? name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}