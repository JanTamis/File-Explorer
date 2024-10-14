using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using FileExplorer.Controls;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using FileExplorer.Interfaces;
using FileExplorer.Resources;
using Humanizer;
using Material.Styles.Assists;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.FileType;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.QuickTime;
using MetadataExtractor.Formats.Wav;
using MetadataExtractor.Formats.WebP;
using TextMateSharp.Grammars;
using Directory = MetadataExtractor.Directory;

namespace FileExplorer.DisplayViews;

public partial class Gallery : UserControl, ISelectableControl, IFileViewer, INotifyPropertyChanged
{
	private int _anchorIndex;

	private bool _isShiftPressed;
	private bool _isCtrlPressed;

	private string _title = String.Empty;
	private string _subTitle = String.Empty;

	private ObservableRangeCollection<IFileItem> _items;
	private ObservableRangeCollection<ProperyData> _properties = new();

	private Task<Control?> _selectionControl;
	private Task<Control?> _selectionControlSmall;

	private readonly List<byte[]> _encodings = Encoding
		.GetEncodings()
		.Select(s => s.GetEncoding().GetPreamble())
		.ToList();

	public new event PropertyChangedEventHandler? PropertyChanged = delegate { };

	public ObservableRangeCollection<IFileItem> Items
	{
		get => _items;
		set => OnPropertyChanged(ref _items, value);
	}

	public Task<Control?> SelectionControl
	{
		get => _selectionControl;
		set => OnPropertyChanged(ref _selectionControl, value);
	}

	public Task<Control?> SelectionControlSmall
	{
		get => _selectionControlSmall;
		set => OnPropertyChanged(ref _selectionControlSmall, value);
	}

	public string Title
	{
		get => _title;
		set => OnPropertyChanged(ref _title, value);
	}

	public string SubTitle
	{
		get => _subTitle;
		set => OnPropertyChanged(ref _subTitle, value);
	}

	public IItemProvider Provider { get; set; }

	public event Action<IFileItem> PathChanged = delegate { };
	public event Action<int> SelectionChanged = delegate { };

	public Gallery()
	{
		InitializeComponent();
		DataContext = this;

		DoubleTappedEvent.Raised.Subscribe(e =>
		{
			if (e.Item1 is ToggleButton { DataContext: IFileItem model, })
			{
				PathChanged(model);
			}
		});

		KeyDownEvent.Raised.Subscribe(e =>
		{
			if (e.Item2 is KeyEventArgs args)
			{
				_isShiftPressed = args.KeyModifiers.HasFlag(KeyModifiers.Shift);
				_isCtrlPressed = args.KeyModifiers.HasFlag(KeyModifiers.Control);
			}
		});

		KeyUpEvent.Raised.Subscribe(e =>
		{
			if (e.Item2 is KeyEventArgs args)
			{
				_isShiftPressed = args.KeyModifiers.HasFlag(KeyModifiers.Shift);
				_isCtrlPressed = args.KeyModifiers.HasFlag(KeyModifiers.Control);
			}
		});

		fileList.ElementPrepared += Grid_ElementPrepared;
		fileList.ElementClearing += Grid_ElementClearing;

		fileList.KeyDown += Grid_KeyDown;

		SelectionChanged += count =>
		{
			SelectionControl = GetSelectionControl(count);
			SelectionControlSmall = GetSelectionControlSmall(count, 64);

			switch (count)
			{
				case 0:
					Title = String.Empty;
					SubTitle = String.Empty;
					break;

				case 1:
				{
					var file = Items.FirstOrDefault(f => f.IsSelected);

					if (file is not null)
					{
						Title = file.Name;

						if (file.IsFolder)
						{
							SubTitle = ResourceDefault.Folder;
						}
						else
						{
							SubTitle = $"{file.Extension.AsSpan(1)} {ResourceDefault.File} - {file.Size.Bytes()}";
						}
					}

					break;
				}

				default:
					var fileCount = 0;
					var folderCount = 0;

					foreach (var item in Items)
					{
						if (!item.IsSelected)
							continue;

						if (item.IsFolder)
						{
							folderCount++;
						}
						else
						{
							fileCount++;
						}
					}

					Title = $"{count} {ResourceDefault.Items}";

					var fileText = fileCount == 1
						? ResourceDefault.File
						: ResourceDefault.Files;

					var folderText = folderCount == 1
						? ResourceDefault.Folder
						: ResourceDefault.Folders;

					if (fileCount > 0 && folderCount > 0)
					{
						SubTitle = $"{fileCount} {fileText}, {folderCount} {folderText}";
					}
					else if (fileCount > 0)
					{
						SubTitle = $"{fileCount} {fileText}";
					}
					else if (folderCount > 0)
					{
						SubTitle = $"{folderCount} {folderText}";
					}

					break;
			}

			_properties.Clear();
			_properties.AddRange(GetProperties(count).DistinctBy(d => d.Name));
		};
	}

	public Gallery(IItemProvider provider, ObservableRangeCollection<IFileItem> items) : this()
	{
		Provider = provider;
		Items = items;

		// SelectionChanged.Invoke(items.Count(c => c.IsSelected));

		items.CountChanged += count =>
		{
			if (count == 0)
			{
				SelectionControl = Task.FromResult<Control?>(null);
				SelectionControlSmall = Task.FromResult<Control?>(null);
				Title = String.Empty;
				SubTitle = String.Empty;
				_properties.Clear();

				_anchorIndex = 0;
			}
		};

		propertyList.Source = new FlatTreeDataGridSource<ProperyData>(_properties)
		{
			Columns =
			{
				new TemplateColumn<ProperyData>(null, new FuncDataTemplate<ProperyData>(_ => true, x =>
					new TextBlock
					{
						Text = x?.Name ?? String.Empty,
						Margin = new Thickness(10, 0, 0, 0),
						Opacity = 0.75,
						VerticalAlignment = VerticalAlignment.Center,
						TextAlignment = TextAlignment.Left,
					}), width: GridLength.Auto),
				new TemplateColumn<ProperyData>(null, new FuncDataTemplate<ProperyData>(_ => true, x => x?.Value ?? new TextBlock()), width: GridLength.Star),
			},
		};
	}

	public void SelectAll()
	{
		foreach (var item in Items)
		{
			item.IsSelected = true;
		}

		SelectionChanged?.Invoke(Items.Count);
	}

	public void SelectNone()
	{
		foreach (var item in Items)
		{
			item.IsSelected = false;
		}

		SelectionChanged?.Invoke(Items.Count);
	}

	public void SelectInvert()
	{
		foreach (var item in Items)
		{
			item.IsSelected ^= true;
		}

		SelectionChanged?.Invoke(Items.Count);
	}

	private void Grid_KeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key is Key.A && e.KeyModifiers is KeyModifiers.Control)
		{
			foreach (var file in Items.Where(x => !x.IsSelected))
			{
				file.IsSelected = true;
			}

			SelectionChanged?.Invoke(Items.Count);
		}

		if (e.Key.HasFlag(Key.Right))
		{
			_anchorIndex = Math.Min(_anchorIndex + 1, Items.Count - 1);

			_anchorIndex = IFileViewer.UpdateSelection(
				this,
				_anchorIndex,
				_anchorIndex,
				out var amount,
				true,
				_isShiftPressed,
				_isCtrlPressed);

			SelectionChanged?.Invoke(amount);
		}

		if (e.Key.HasFlag(Key.Left))
		{
			_anchorIndex = Math.Max(0, _anchorIndex - 1);

			_anchorIndex = IFileViewer.UpdateSelection(
				this,
				_anchorIndex,
				_anchorIndex,
				out var amount,
				true,
				_isShiftPressed,
				_isCtrlPressed);

			SelectionChanged?.Invoke(amount);
		}
	}

	private void Grid_ElementClearing(object? sender, ItemsRepeaterElementClearingEventArgs e)
	{
		if (e.Element is ToggleButton item)
		{
			item.DoubleTapped -= Item_DoubleTapped;
			item.PointerPressed -= Item_PointerPressed;
		}
	}

	private void Item_PointerPressed(object? sender, RoutedEventArgs e)
	{
		if (sender is ToggleButton { DataContext: IFileItem model, } item)
		{
			var index = Items.IndexOf(model);

			_anchorIndex = IFileViewer.UpdateSelection(
				this,
				_anchorIndex,
				index,
				out var amount,
				true,
				_isShiftPressed,
				_isCtrlPressed);

			SelectionChanged?.Invoke(amount);
		}
	}

	private void Grid_ElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
	{
		if (e.Element is ToggleButton { DataContext: IFileItem model, } item)
		{
			item.DoubleTapped += Item_DoubleTapped;
			item.Click += Item_PointerPressed;

			model.IsVisible = true;
		}
	}

	private void Item_DoubleTapped(object? sender, RoutedEventArgs e)
	{
		if (sender is ToggleButton { DataContext: IFileItem model, })
		{
			PathChanged(model);
		}
	}

	private void OnPropertyChanged<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	private async Task<Control?> GetSelectionControl(int count)
	{
		if (count == 1)
		{
			var file = Items.FirstOrDefault(f => f.IsSelected);
			await using var stream = file!.GetStream();

			if (!file.IsFolder && await IsTextFile(stream, file.Extension))
			{
				return await GetTextControl(stream, file.Extension);
			}
		}

		var panel = new Panel
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(20),
		};

		var effect = new DropShadowEffect
		{
			Color = ShadowProvider.MaterialShadowColor,
			BlurRadius = 8,
			OffsetY = 0,
		}.ToImmutable();

		foreach (var item in Items)
		{
			if (!item.IsSelected)
			{
				continue;
			}

			if (count > 3)
			{
				count--;
				continue;
			}

			var image = new Image
			{
				Source = await ThumbnailProvider.GetFileImage(item, Provider, Int32.MaxValue, () => true),
				RenderTransform = new RotateTransform
				{
					Angle = Random.Shared.NextDouble() * 20 - 10,
				},
				RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
			};

			panel.Children.Add(image);
		}

		if (panel.Children.Any())
		{
			panel.Children[^1].RenderTransform = null;
		}

		for (var i = 1; i < panel.Children.Count; i++)
		{
			panel.Children[i].Effect = effect;
		}

		return panel;
	}

	private async Task<Control?> GetSelectionControlSmall(int count, int size = Int32.MaxValue)
	{
		var panel = new Panel();
		var effect = new DropShadowEffect
		{
			Color = ShadowProvider.MaterialShadowColor,
			BlurRadius = 8,
			OffsetY = 0,
		}.ToImmutable();

		foreach (var item in Items)
		{
			if (!item.IsSelected)
			{
				continue;
			}

			if (count > 3)
			{
				count--;
				continue;
			}

			var image = new Image
			{
				Source = await ThumbnailProvider.GetFileImage(item, Provider, size, () => true),
				RenderTransform = new RotateTransform
				{
					Angle = Random.Shared.NextDouble() * 20 - 10,
				},
				RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
			};

			panel.Children.Add(image);
		}

		if (panel.Children.Any())
		{
			panel.Children[^1].RenderTransform = null;
		}

		for (var i = 1; i < panel.Children.Count; i++)
		{
			panel.Children[i].Effect = effect;
		}

		return panel;
	}

	private IEnumerable<ProperyData> GetProperties(int count)
	{
		if (count == 1)
		{
			var file = Items.FirstOrDefault(f => f.IsSelected);

			if (file is not null)
			{
				var extension = file.Extension;
				yield return new ProperyData(ResourceDefault.CreatedOn, new DateTimeTextBlock { DateTime = file.CreatedOn });
				yield return new ProperyData(ResourceDefault.Edited, new DateTimeTextBlock { DateTime = file.EditedOn });
				// yield return new ProperyData(ResourceDefault.Modified, file.Modified.ToString());

				if (ThumbnailProvider.FileTypes.TryGetValue("Jpeg", out var files) && files.Contains(extension) ||
				    ThumbnailProvider.FileTypes.TryGetValue("Png", out files) && files.Contains(extension) ||
				    ThumbnailProvider.FileTypes.TryGetValue("RawImage", out files) && files.Contains(extension) ||
				    ThumbnailProvider.FileTypes.TryGetValue("Audio", out files) && files.Contains(extension) ||
				    ThumbnailProvider.FileTypes.TryGetValue("Video", out files) && files.Contains(extension) ||
				    String.Equals(".wav", extension, StringComparison.OrdinalIgnoreCase) ||
				    String.Equals(".webp", extension, StringComparison.OrdinalIgnoreCase))
				{
					IReadOnlyList<Directory> directories;

					using (var stream = file.GetStream())
					{
						directories = ImageMetadataReader.ReadMetadata(stream);
					}

					foreach (var directory in directories)
					{
						var enumerable = directory switch
						{
							ExifDirectoryBase exifDirectory            => GetExifData(exifDirectory),
							JpegDirectory jpegDirectory                => GetJpegData(jpegDirectory),
							PngDirectory pngDirectory                  => GetPngData(pngDirectory),
							WebPDirectory webPDirectory                => GetWebPData(webPDirectory),
							QuickTimeTrackHeaderDirectory movDirectory => GetMovData(movDirectory),
							FileTypeDirectory fileTypeDirectory        => GetFileTypeData(fileTypeDirectory),
							WavFormatDirectory wavFormatDirectory      => GetWavData(wavFormatDirectory),
							_                                          => Enumerable.Empty<ProperyData>(),
						};

						foreach (var data in enumerable)
						{
							yield return data;
						}
					}
				}
			}
		}

		yield break;

		IEnumerable<ProperyData> GetWavData(WavFormatDirectory directory)
		{
			if (directory.TryGetInt32(WavFormatDirectory.TagSamplesPerSec, out var sampleLength))
			{
				yield return new ProperyData(ResourceDefault.SampleRate, $"{sampleLength / 1000} kHz");
			}

			if (directory.TryGetInt32(WavFormatDirectory.TagBitsPerSample, out var bitsPerSample))
			{
				yield return new ProperyData(ResourceDefault.BitsPerSample, $"{bitsPerSample}");
			}
		}

		IEnumerable<ProperyData> GetWebPData(WebPDirectory directory)
		{
			if (directory.TryGetInt32(WebPDirectory.TagImageWidth, out var width) &&
			    directory.TryGetInt32(WebPDirectory.TagImageHeight, out var height))
			{
				yield return new ProperyData(ResourceDefault.Dimensions, $"{width}x{height} px");
			}

			if (directory.TryGetBoolean(WebPDirectory.TagHasAlpha, out var hasAlpha))
			{
				yield return new ProperyData(ResourceDefault.HasAlpha, hasAlpha ? ResourceDefault.Yes : ResourceDefault.No);
			}

			if (directory.TryGetBoolean(WebPDirectory.TagIsAnimation, out var isAnimation))
			{
				yield return new ProperyData(ResourceDefault.IsAnimation, isAnimation ? ResourceDefault.Yes : ResourceDefault.No);
			}
		}

		IEnumerable<ProperyData> GetFileTypeData(FileTypeDirectory directory)
		{
			if (directory.GetString(FileTypeDirectory.TagDetectedFileMimeType) is { Length: > 0 } mimeType)
			{
				yield return new ProperyData(ResourceDefault.MimeType, mimeType);
			}
		}

		IEnumerable<ProperyData> GetMovData(QuickTimeTrackHeaderDirectory directory)
		{
			if (directory.TryGetInt32(QuickTimeTrackHeaderDirectory.TagWidth, out var width) &&
			    directory.TryGetInt32(QuickTimeTrackHeaderDirectory.TagHeight, out var height))
			{
				yield return new ProperyData(ResourceDefault.Dimensions, $"{width}x{height} px");
			}

			if (directory.TryGetInt64(QuickTimeTrackHeaderDirectory.TagDuration, out var duration))
			{
				yield return new ProperyData(ResourceDefault.Duration, new TimeSpanTextBlock()
				{
					TimeSpan = TimeSpan.FromMilliseconds(duration),
				});
			}
		}

		IEnumerable<ProperyData> GetJpegData(JpegDirectory directory)
		{
			var width = directory.GetImageWidth();
			var height = directory.GetImageHeight();

			yield return new ProperyData(ResourceDefault.Dimensions, $"{width}x{height} px");

			if (directory.TryGetInt32(JpegDirectory.TagDataPrecision, out var dataPrecision))
			{
				yield return new ProperyData(ResourceDefault.DataPrecision, $"{dataPrecision} bits");
			}
		}

		IEnumerable<ProperyData> GetPngData(PngDirectory directory)
		{
			if (directory.TryGetInt32(PngDirectory.TagImageWidth, out var width) &&
			    directory.TryGetInt32(PngDirectory.TagImageHeight, out var height))
			{
				yield return new ProperyData(ResourceDefault.Dimensions, $"{width}x{height} px");
			}

			if (directory.TryGetInt32(PngDirectory.TagBitsPerSample, out var bitsPerSample))
			{
				yield return new ProperyData(ResourceDefault.DataPrecision, $"{bitsPerSample} bits");
			}
		}

		IEnumerable<ProperyData> GetExifData(ExifDirectoryBase directory)
		{
			if (directory.TryGetInt32(ExifDirectoryBase.TagImageWidth, out var width) &&
			    directory.TryGetInt32(ExifDirectoryBase.TagImageHeight, out var height))
			{
				yield return new ProperyData(ResourceDefault.Dimensions, $"{width}x{height}");
			}

			if (directory.TryGetInt32(ExifDirectoryBase.TagXResolution, out var widhtResolution) &&
			    directory.TryGetInt32(ExifDirectoryBase.TagYResolution, out var heightResolution))
			{
				yield return new ProperyData(ResourceDefault.Resolution, $"{widhtResolution}x{heightResolution} dpi");
			}

			if (directory.TryGetInt32(ExifDirectoryBase.TagBitsPerSample, out var bitsPerSample))
			{
				yield return new ProperyData(ResourceDefault.DataPrecision, $"{bitsPerSample} bits");
			}

			if (directory.GetString(ExifDirectoryBase.TagMake) is { Length: > 0 } make)
			{
				yield return new ProperyData(ResourceDefault.Make, make);
			}

			if (directory.GetString(ExifDirectoryBase.TagModel) is { Length: > 0 } model)
			{
				yield return new ProperyData(ResourceDefault.Model, model);
			}

			if (directory.TryGetDouble(ExifDirectoryBase.TagAperture, out var aperture))
			{
				yield return new ProperyData(ResourceDefault.Aperture, $"{aperture}");
				yield return new ProperyData(ResourceDefault.FStop, $"f/{Math.Round(Math.Pow(Math.Sqrt(2), aperture), 1, MidpointRounding.ToZero):N1}");
			}

			if (directory.TryGetDouble(ExifDirectoryBase.TagShutterSpeed, out var shutterSpeed))
			{
				yield return new ProperyData(ResourceDefault.ShutterSpeed, $"1/{Math.Exp(shutterSpeed * Math.Log(2)):N0}");
			}

			if (directory.TryGetInt32(ExifDirectoryBase.TagIsoSpeed, out var iso))
			{
				yield return new ProperyData(ResourceDefault.ISOSpeed, $"{iso}");
			}
		}
	}

	private async Task<bool> IsTextFile(Stream file, string extension)
	{
		if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		
		var options = new RegistryOptions(ThemeName.Dark);

		if (options.GetLanguageByExtension(extension) is not null)
		{
			return true;
		}

		var buffer = new byte[4];
		await file.ReadAsync(buffer);

		if (Equals(buffer, Encoding.UTF8) ||
		    Equals(buffer, Encoding.UTF32) ||
		    Equals(buffer, Encoding.Unicode) ||
		    Equals(buffer, Encoding.BigEndianUnicode))
		{
			return true;
		}

		return false;
		
		bool Equals(ReadOnlySpan<byte> data, Encoding encoding)
		{
			return encoding.Preamble.SequenceEqual(data.Slice(encoding.Preamble.Length));
		}
	}

	private async Task<Control?> GetTextControl(Stream stream, string extension)
	{
		stream.Seek(0, SeekOrigin.Begin);

		using var reader = new StreamReader(stream);

		var text = await reader.ReadToEndAsync();
		
		var texteditor = new TextEditor
		{
			IsEnabled = false,
			ShowLineNumbers = true,
			Text = text,
			Options = new TextEditorOptions
			{
				EnableEmailHyperlinks = false,
				EnableHyperlinks = false,
			}
		};

		var options = new RegistryOptions(ThemeName.Dark);
		var installation = texteditor.InstallTextMate(options);
		
		installation.SetGrammar(options.GetScopeByExtension(extension));

		return new ScrollViewer
		{
			Content = texteditor,
		};
	}

	private class ProperyData
	{
		public string Name { get; }
		public TextBlock Value { get; }

		public ProperyData(string name, TextBlock value)
		{
			Name = name;
			Value = value;

			value.Margin = new Thickness(0, 0, 10, 0);
			value.VerticalAlignment = VerticalAlignment.Center;
			value.TextTrimming = TextTrimming.WordEllipsis;
			value.TextAlignment = TextAlignment.Right;
		}

		public ProperyData(string name, string value) 
			: this(name, new TextBlock { Text = value })
		{
		}
	}
}