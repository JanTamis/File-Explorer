using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using System.IO;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls.Skia;
using Avalonia.Platform;
using FileExplorer.Core.Extensions;
using FileExplorer.Core.Interfaces;
using FileExplorer.Models;
using Svg;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using SvgImage = Avalonia.Svg.Skia.SvgImage;

namespace FileExplorer.Helpers;
#pragma warning disable CA1416

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public static class ThumbnailProvider
{
	private static readonly ConcurrentDictionary<int, IImage> Images = new();

	public static readonly Dictionary<string, FrozenSet<string>> FileTypes = new()
	{
		{ "Word", new[] { ".doc", ".docx", ".docm", ".dotx", ".dotm", ".docb", ".odt", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "PowerPoint", new[] { ".ppt", ".pptx", ".pptm", ".potx", ".potm", ".ppam", ".ppsx", ".ppsm", ".sldx", ".sldm", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "Excel", new[] { ".xls", ".xlsx", ".xlsm", ".xltx", ".xltm", ".xlsb", ".xlam", ".ods", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "Access", new[] { ".accdb", ".accde", ".accdt", ".accdr", ".mdb", ".mde", ".mda", ".mdt", ".mdw", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },

		{ "Font", new[] { ".ttf", ".otf", ".woff", ".woff2", ".eot" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },

		{ "Jpeg", new[] { ".jpg", ".jpeg", ".jpe", ".jfif", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "Png", new[] { ".png", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "RawImage", new[] { ".arw", ".cr2", ".cr3", ".dng", ".nef", ".orf", ".raf", ".rw2", ".srw", ".pef", ".3fr", ".mos", ".raw", ".x3f", ".sr2", ".erf", ".fff", ".mfw", ".nrw", ".rwl", ".cap", ".iiq", ".eip", ".kdc", ".fff", ".mef", ".mdc", ".ptx", ".pxn", ".r3d", ".raw", ".rwl", ".rwz", ".srw", ".x3f", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },

		{ "Archive", new[] { ".zip", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".lzma", ".lz", ".lzo", ".z", ".arj", ".cab", ".chm", ".deb", ".lzh", ".rpm", ".udf", ".wim", ".xar", ".zoo", ".war", ".ear", ".sar", ".par", ".tar.gz", ".tar.bz2", ".tar.xz", ".tar.lzma", ".tar.lzo", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },

		{ "Audio", new[] { ".mp3", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "Video", new[] { ".mp4", ".mov", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },

		{ "Web", new[] { ".url", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },

		{ "AdobeIllustrator", new[] { ".ai", ".eps", ".ait", ".aia", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "AdobeXd", new[] { ".xd", ".xdp", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "AdobeAnimate", new[] { ".fla", ".swf", ".jsfl", ".flv", ".xfl", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "AdobeFireworks", new[] { ".fw", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "AdobeInDesign", new[] { ".indd", ".indt", ".idml", ".inx", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "AdobeFramemaker", new[] { ".fm", ".mif", ".fml", ".fmtemplate", ".fmstyles", ".fmindex", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "AdobeAfterEffects", new[] { ".aep", ".aet", ".aepx", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },

		{ "Android", new[] { ".aidl" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		
		{ "Xml", new[] { ".xml", ".xsd", ".xsl", ".xslt", ".xps", ".oxps", ".axaml", ".xaml", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "CPlusPlus", new[] { ".cpp", ".h", ".cc", ".cxx", ".h", ".hpp", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "Java", new[] { ".java", ".class", ".jar", ".javadoc", ".javap", ".pde", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "HTML", new[] { ".html", ".htm", ".shtml", ".shtm", ".xhtml", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "CSS", new[] { ".css", ".scss", ".less", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "JavaScript", new[] { ".js", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },

		{ "Data", new[] { ".bin", ".dmp", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) },
		{ "Executable", new[] { ".exe", ".app", }.ToFrozenSet(StringComparer.OrdinalIgnoreCase) }
	};

	private static readonly Dictionary<string, IImage> FileImages = new()
	{
		{ "Word", new SKPictureImage { Source = WordPicture.Picture } },
		{ "PowerPoint", new SKPictureImage { Source = PowerPointPicture.Picture } },
		{ "Access", new SKPictureImage { Source = AccessPicture.Picture } },

		{ "Font", new SKPictureImage { Source = FontPicture.Picture } },

		{ "Jpeg", new SKPictureImage { Source = JpegPicture.Picture } },
		{ "Png", new SKPictureImage { Source = PngPicture.Picture } },
		{ "RawImage", new SKPictureImage { Source = RawImagePicture.Picture } },
		
		{ "Archive", new SKPictureImage { Source = ArchivePicture.Picture } },

		{ "Audio", new SKPictureImage { Source = AudioPicture.Picture } },
		{ "Video", new SKPictureImage { Source = VideoPicture.Picture } },

		{ "Web", new SKPictureImage { Source = WebPicture.Picture } },

		{ "AdobeIllustrator", new SKPictureImage { Source = AdobeIllustratorPicture.Picture } },
		{ "AdobeXd", new SKPictureImage { Source = AdobeXdPicture.Picture } },
		{ "AdobeAnimate", new SKPictureImage { Source = AdobeAnimatePicture.Picture } },
		{ "AdobeFireworks", new SKPictureImage { Source = AdobeFireworksPicture.Picture } },
		{ "AdobeInDesign", new SKPictureImage { Source = AdobeInDesignPicture.Picture } },
		{ "AdobeFramemaker", new SKPictureImage { Source = AdobeFramemakerPicture.Picture } },
		{ "AdobeAfterEffects", new SKPictureImage { Source = AdobeAfterEffectsPicture.Picture } },

		{ "Android", new SKPictureImage { Source = AndroidPicture.Picture } },

		{ "Xml", new SKPictureImage { Source = XmlPicture.Picture } },
		{ "CPlusPlus", new SKPictureImage { Source = CPlusPlusPicture.Picture } },
		{ "Java", new SKPictureImage { Source = JavaPicture.Picture } },
		{ "HTML", new SKPictureImage { Source = HTMLPicture.Picture } },
		{ "CSS", new SKPictureImage { Source = CSSPicture.Picture } },
		{ "JavaScript", new SKPictureImage { Source = JavaScriptPicture.Picture } },

		{ "Data", new SKPictureImage { Source = DataPicture.Picture } },
		{ "Executable", new SKPictureImage { Source = ExecutablePicture.Picture } }
	};

	public async static Task<IImage?> GetFileImage(IFileItem? model, IItemProvider provider, int size, Func<bool>? shouldReturnImage = null)
	{
		if (model is null || shouldReturnImage is not null && !shouldReturnImage())
		{
			return null;
		}

		if (OperatingSystem.IsWindows() && model is FileModel)
		{
			return await ImageFromFileModel(model, size, shouldReturnImage);
		}

		return await ImageFromData(model, provider, size, shouldReturnImage);
	}

	private async static Task<IImage?> ImageFromData(IFileItem model, IItemProvider provider, int size, Func<bool>? shouldReturnImage)
	{
		return await await Runner.RunSecundairy(() => model?.GetPath((path, imageSize) =>
		{
			if (model.IsFolder)
			{
				if (TryGetFromKnownFolder(path, out var result))
				{
					return result;
				}

				if (TryGetFromDrive(model, out result))
				{
					return result;
				}

				return Dispatcher.UIThread.InvokeAsync<IImage?>(() => new SKPictureImage
				{
					Source = provider.HasItems(model)
						? FolderFilesPicture.Picture
						: FolderPicture.Picture,
				}).GetTask();
			}

			if (model.Extension is { Length: > 1, } extension)
			{
				if ((shouldReturnImage is null || shouldReturnImage()) && TryGetFromBitmap(model, extension, size, out var result))
				{
					return result;
				}
				
				if ((shouldReturnImage is null || shouldReturnImage()) && TryGetFromSvg(model, extension, out result))
				{
					return result;
				}
				
				if ((shouldReturnImage is null || shouldReturnImage()) && TryGetFromKnownExtension(extension, out result))
				{
					return result;
				}

				return GetFromExtension(extension.AsSpan(1));
			}

			return Dispatcher.UIThread.InvokeAsync<IImage?>(() => new SKPictureImage
			{
				Source = FilePicture.Picture,
			}).GetTask();
		}, size) ?? Task.FromResult<IImage?>(null)).ConfigureAwait(false);
	}

	private async static Task<IImage?> ImageFromFileModel(IFileItem model, int size, Func<bool>? shouldReturnImage)
	{
		return await Runner.RunSecundairy(() => model?.GetPath((path, imageSize) =>
		{
			Bitmap? image = null;

			if (shouldReturnImage is null || shouldReturnImage())
			{
				image = WindowsThumbnailProvider.GetThumbnail(path, imageSize, imageSize, ThumbnailOptions.ThumbnailOnly, () => size is < 64 and >= 32);
			}

			if (image is null && (shouldReturnImage is null || shouldReturnImage()))
			{
				image = WindowsThumbnailProvider.GetThumbnail(path, imageSize, imageSize, ThumbnailOptions.IconOnly, () => true);
			}

			return image;
		}, size)).ConfigureAwait(false);
	}

	public async static Task<IImage?> GetFileImage(FileSystemTreeItem? model, int size, Func<bool>? shouldReturnImage = null)
	{
		if (model is null || shouldReturnImage is not null && !shouldReturnImage())
		{
			return null;
		}

		if (OperatingSystem.IsWindows())
		{
			return await Runner.RunSecundairy(() => model?.GetPath((path, imageSize) =>
			{
				Bitmap? image = null!;

				if (shouldReturnImage is null || shouldReturnImage())
				{
					image = WindowsThumbnailProvider.GetThumbnail(path, imageSize, imageSize, ThumbnailOptions.ThumbnailOnly, () => size is < 64 and >= 32);
				}

				if (image is null && (shouldReturnImage is null || shouldReturnImage()))
				{
					image = WindowsThumbnailProvider.GetThumbnail(path, imageSize, imageSize, ThumbnailOptions.IconOnly, () => true);
				}

				return image;
			}, size)).ConfigureAwait(false);
		}

		return await await Runner.RunSecundairy(() => model?.GetPath((path, imageSize) =>
		{
			if (model.IsFolder)
			{
				if (TryGetFromKnownFolder(path, out var result) && (shouldReturnImage is null || shouldReturnImage()))
				{
					return result;
				}

				if (TryGetFromDrive(model, out result) && (shouldReturnImage is null || shouldReturnImage()))
				{
					return result;
				}

				return Task.FromResult<IImage?>(new SKPictureImage
				{
					Source = model.HasChildren
						? FolderFilesPicture.Picture
						: FolderPicture.Picture,
				});
			}

			var extension = Path.GetExtension(path);

			if (extension is { Length: > 1, } && (shouldReturnImage is null || shouldReturnImage()))
			{
				return GetFromExtension(extension.Slice(1));
			}

			return Task.FromResult<IImage?>(new SKPictureImage
			{
				Source = FilePicture.Picture,
			});
		}, size) ?? Task.FromResult<IImage?>(null));
	}

	public static Task<IImage?> GetImageAsync(ReadOnlySpan<char> key)
	{
		var path = new Uri($"avares://FileExplorer/Assets/Icons/{key}.svg");
		
		if (key.IsEmpty || !AssetLoader.Exists(path))
		{
			return Dispatcher.UIThread.InvokeAsync<IImage?>(() =>new SKPictureImage
			{
				Source = FilePicture.Picture,
			}).GetTask();
		}

		var hash = String.GetHashCode(key);
		
		if (!Images.TryGetValue(hash, out var image))
		{
			var source = new SvgSource();
			using var stream = AssetLoader.Open(path);

			source.Load(stream);

			//var source = SvgSource.Load<SvgSource>($"avares://FileExplorer/Assets/Icons/{key}.svg", null);

			return Dispatcher.UIThread.InvokeAsync(() => new SvgImage
			{
				Source = source
			}, DispatcherPriority.Background).GetTask().ContinueWith((x, keyHash) =>
			{
				Images.TryAdd(Unsafe.Unbox<int>(keyHash!), x.Result);
				return (IImage?) x.Result;
			}, hash);
		}

		return Task.FromResult((IImage?) image);
	}

	public static IImage? GetImage(ReadOnlySpan<char> key)
	{
		if (key.IsEmpty)
		{
			key = "File";
		}

		var hash = String.GetHashCode(key);

		if (!Images.TryGetValue(hash, out var image))
		{
			var source = SvgSource.Load<SvgSource>($"avares://FileExplorer/Assets/Icons/{key}.svg", null);

			image = new SvgImage
			{
				Source = source
			};

			Images.TryAdd(hash, image);
		}

		return image;
	}

	private static bool TryGetFromDrive(IFileItem fileItem, out Task<IImage?> result)
	{
		if (fileItem is FileModel { IsRoot: false, })
		{
			return TryGetFromDrivePath(new string(fileItem.Name[0], 1), out result);
		}

		result = Task.FromResult<IImage?>(null);
		return false;
	}

	private static bool TryGetFromDrive(FileSystemTreeItem treeItem, out Task<IImage?> result)
	{
		if (treeItem is { HasParent: true, })
		{
			return TryGetFromDrivePath(new string(treeItem.Value[0], 1), out result);
		}

		result = Task.FromResult<IImage?>(null);
		return false;
	}

	private static bool TryGetFromDrivePath(string path, out Task<IImage?> result)
	{
		var driveInfo = new DriveInfo(path);

		if (driveInfo.IsReady)
		{
			result = GetImageAsync(Enum.GetName(driveInfo.DriveType));
			return true;
		}

		result = Task.FromResult<IImage?>(null);
		return false;
	}

	private static bool TryGetFromKnownFolder(ReadOnlySpan<char> path, out Task<IImage?> result)
	{
		if (OperatingSystem.IsWindows())
		{
			foreach (var folder in Enum.GetValues<KnownFolder>())
			{
				var folderText = Enum.GetName(folder);

				if (folderText is not null && path.SequenceEqual(KnownFolders.GetPath(folder)))
				{
					result = GetImageAsync(folderText);
					return true;
				}
			}
		}

		result = Task.FromResult<IImage?>(null);
		return false;
	}

	private static bool TryGetFromBitmap(IFileItem fileItem, string extension, int size, out Task<IImage?> result)
	{
		if (FileTypes.TryGetValue("Jpeg", out var files) && files.Contains(extension) ||
		    FileTypes.TryGetValue("Png", out files) && files.Contains(extension))
		{
			// result = Runner.RunSecundairy(() =>
			// {
			// 	using var fileStream = File.OpenRead(fileItem.GetPath());
			// 	return (IImage?) Bitmap.DecodeToWidth(fileStream, size, BitmapInterpolationMode.MediumQuality);
			// });

			result = Runner.RunSecundairy<IImage?>(() =>
			{
				var fullImage = new Bitmap(fileItem.GetPath());

				if (size == Int32.MaxValue)
				{
					return fullImage;
				}
				
				var newHeight = fullImage.Size.Width > size
					? size / fullImage.Size.Width * fullImage.Size.Height
					: fullImage.Size.Height;

				if (Math.Abs(fullImage.Size.Width - fullImage.Size.Height) < Single.Epsilon)
				{
					newHeight = size;
				}
				
				var thumbnail = fullImage.CreateScaledBitmap(new PixelSize(size, (int) newHeight), BitmapInterpolationMode.MediumQuality);
				
				fullImage.Dispose();
				return thumbnail;
			});

			return true;
		}

		result = Task.FromResult<IImage?>(null);
		return false;
	}

	private static bool TryGetFromSvg(IFileItem fileItem, ReadOnlySpan<char> extension, out Task<IImage?> result)
	{
		if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
		{
			var source = SvgSource.Load<SvgSource>(fileItem.GetPath(), null);

			result = Dispatcher.UIThread.InvokeAsync(() => (IImage?) new SvgImage
			{
				Source = source
			}, DispatcherPriority.Background).GetTask();

			return true;
		}

		result = Task.FromResult<IImage?>(null);
		return false;
	}

	private static bool TryGetFromKnownExtension(string extension, out Task<IImage?> result)
	{
		foreach (var (fileName, extensions) in FileTypes)
		{
			if (extensions.Contains(extension))
			{
				if (FileImages.TryGetValue(fileName, out var image))
				{
					result = Task.FromResult<IImage?>(image);
				}
				else
				{
					result = GetImageAsync(fileName);					
				}
				
				return true;
			}
		}

		result = Task.FromResult<IImage?>(null);
		return false;
	}

	private static Task<IImage?> GetFromExtension(ReadOnlySpan<char> extension)
	{
		Span<char> extensionSpan = stackalloc char[extension.Length];
		extensionSpan = extensionSpan.Slice(0, extension.ToLower(extensionSpan, CultureInfo.CurrentCulture));

		return GetImageAsync(extensionSpan);
	}
}

#pragma warning restore CA1416