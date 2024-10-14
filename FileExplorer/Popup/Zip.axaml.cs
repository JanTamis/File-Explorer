using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Controls;
using DialogHostAvalonia;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using FileExplorer.Resources;
using Humanizer;
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
	private string _speed;
	private bool _isRunning;
	
	private long _timestamp;

	private long _previousSpeed;
	
	private long _totalSize;

	private CancellationTokenSource _cancellationTokenSource;

	private EtaCalculator _etaCalculator;

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
			
			TotalSizeText = TotalSize.Bytes().ToString();

			OnPropertyChanged(nameof(TotalSize));
			OnPropertyChanged(nameof(TotalSizeText));
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

	public string Speed
	{
		get => _speed;
		set => OnPropertyChanged(ref _speed, value);
	}

	public string FileName
	{
		get => _fileName;
		set => OnPropertyChanged(ref _fileName, value);
	}

	public bool IsRunning
	{
		get => _isRunning;
		set
		{
			OnPropertyChanged(ref _isRunning, value);
			OnPropertyChanged(nameof(CloseText));
		}
	}

	public string CloseText => IsRunning
		? ResourceDefault.Cancel
		: ResourceDefault.Close;

	public TimeSpan EstimatedTime => _etaCalculator is not null ? _etaCalculator.ETR.Subtract(TimeSpan.FromMilliseconds(_etaCalculator.ETR.Milliseconds)) : TimeSpan.Zero;
	
	public bool HasEstimatedTime => _etaCalculator?.ETAIsAvailable ?? false;

	public TimeSpan ElapsedTime => Stopwatch.GetElapsedTime(_timestamp);

	public long TotalSize { get; private set; }
	public string TotalSizeText { get; private set; }

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
		_etaCalculator = new EtaCalculator(0, TimeSpan.FromMinutes(1));
		_cancellationTokenSource = new CancellationTokenSource();

		await Task.Run(() =>
		{
			using var destinationStream = File.Create(Path.Combine(Folder.GetPath(), FileName));
			var extension = Path.GetExtension(FileName.AsSpan());

			var writer = GetWriterBasedOnExtension(extension, destinationStream);

			if (writer is not null)
			{
				ZipFiles(writer);
				writer.Dispose();
			}
		}, _cancellationTokenSource.Token);

		Close();
	}

	public void Close()
	{
		IsRunning = false;
		OnClose?.Invoke();

		if (DialogHost.IsDialogOpen(null))
		{
			DialogHost.Close(null);
		}
	}

	public async Task Cancel()
	{
		Close();

		if (_cancellationTokenSource is not null)
		{
			await _cancellationTokenSource.CancelAsync();

			if (_cancellationTokenSource.IsCancellationRequested)
			{
				File.Delete(Path.Combine(Folder.GetPath(), FileName));
			}
		}
	}

	private void ZipFiles(IWriter writer)
	{
		_timestamp = Stopwatch.GetTimestamp();
		Task.Run(UpdateEstimatedTime, _cancellationTokenSource.Token);

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
				Task.Run(() => UpdateProgress(file), _cancellationTokenSource.Token);
				writer.Write(selectedFile.Name, file);
			}
		}
	}

	private async Task UpdateEstimatedTime()
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

		while (IsRunning && await timer.WaitForNextTickAsync(_cancellationTokenSource.Token))
		{
			OnPropertyChanged(nameof(EstimatedTime));
			OnPropertyChanged(nameof(HasEstimatedTime));

			OnPropertyChanged(nameof(ElapsedTime));

			Speed = $"{_previousSpeed.Bytes()}/s";

			
			_previousSpeed = 0;
		}
	}

	private async Task UpdateProgress(Stream file)
	{
		const int interval = 25;
		using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(interval));

		var previous = 0L;
		var currentStep = 0;

		while (IsRunning && await timer.WaitForNextTickAsync(_cancellationTokenSource.Token))
		{
			var position = file.Position;

			Progress += position - previous;
			
			_previousSpeed += position - previous;

			_etaCalculator.Update((double) ((decimal) Progress / (decimal) TotalSize));

			previous = position;
			currentStep++;

			if (currentStep >= 1000 / interval)
			{
				currentStep = 0;
			}
		}
	}

	private IWriter? GetWriterBasedOnExtension(ReadOnlySpan<char> extension, Stream destinationStream)
	{
		switch (extension)
		{
			case ".zip":
			case ".zipx":
			case ".cbz":
				return new ZipWriter(destinationStream, new ZipWriterOptions(CompressionType.Deflate)
				{
					DeflateCompressionLevel = CompressionLevel.Default,
					ArchiveEncoding = new ArchiveEncoding(Encoding.UTF8, Encoding.Default),
					UseZip64 = true
				});
			case ".tar":
			case ".taz":
			case ".tgz":
			case ".tb2":
			case ".tbz":
			case ".tbz2":
			case ".tz2":
			case ".tZ":
			case ".taZ":
				return new TarWriter(destinationStream, new TarWriterOptions(CompressionType.GZip, true));
			case ".gz":
				return new GZipWriter(destinationStream, new GZipWriterOptions());
			default:
				return null;
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