using System.Collections.Concurrent;
using System.Collections.Frozen;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using System.IO;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls.Documents;
using FileExplorer.Core.Extensions;
using FileExplorer.Core.Interfaces;
using FileExplorer.Models;

namespace FileExplorer.Helpers;
#pragma warning disable CA1416

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public static class ThumbnailProvider
{
	private static readonly ConcurrentDictionary<int, IImage> Images = new();

	private static FrozenSet<string> _docs = new[] { ".doc", ".docx", ".docm", ".dotx", ".dotm", ".docb", ".odt" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase, true);
	private static FrozenSet<string> _sheets = new[] { ".xls", ".xlsx", ".xlsm", ".xltx", ".xltm", ".xlsb", ".xlam", ".ods" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase, true);
	private static FrozenSet<string> _slides = new[] { ".ppt", ".pptx", ".pptm", ".potx", ".potm", ".ppam", ".ppsx", ".ppsm", ".sldx", ".sldm" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase, true);
	private static FrozenSet<string> _access = new[] { ".accdb", ".accde", ".accdt", ".accdr", ".mdb", ".mde", ".mda", ".mdt", ".mdw" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase, true);
	private static FrozenSet<string> _xml = new[] { ".xml", ".xsd", ".xsl", ".xslt", ".xps", ".oxps", ".axaml", ".xaml" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase, true);
	private static FrozenSet<string> _fonts = new[] { ".ttf", ".otf", ".woff", ".woff2", ".eot" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase, true);
	private static FrozenSet<string> _jpeg = new[] { ".jpg", ".jpeg", ".jpe", ".jfif" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase, true);
	private static FrozenSet<string> _rawImage = new[] { ".cr2" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase, true);


	public static async Task<IImage?> GetFileImage(IFileItem? model, IItemProvider provider, int size, Func<bool>? shouldReturnImage = null)
	{
		if (model is null || shouldReturnImage is not null && !shouldReturnImage())
		{
			return null;
		}

		if (OperatingSystem.IsWindows() && model is FileModel)
		{
			return await ImageFromFileModel(model, size, shouldReturnImage);
		}

		return await ImageFromData(model, provider, size);
	}

	private static async Task<IImage?> ImageFromData(IFileItem model, IItemProvider provider, int size)
	{
		return await await Runner.RunSecundairy(() => model?.GetPath((path, imageSize) =>
		{
			if (model.IsFolder)
			{
				if (OperatingSystem.IsWindows())
				{
					foreach (var folder in Enum.GetValues<KnownFolder>())
					{
						var folderText = Enum.GetName(folder);

						if (folderText is not null && path.SequenceEqual(KnownFolders.GetPath(folder)))
						{
							return GetImageAsync(folderText);
						}
					}
				}

				if (model is FileModel { IsRoot: false })
				{
					var driveInfo = new DriveInfo(new string(model.Name[0], 1));

					if (driveInfo.IsReady)
					{
						return GetImageAsync(Enum.GetName(driveInfo.DriveType));
					}
				}

				if (provider.HasItems(model))
				{
					return GetImageAsync("FolderFiles");
				}
			}
			else
			{
				var extension = model.Extension;

				if (_docs.Contains(extension))
				{
					return GetImageAsync("Word");
				}

				if (_sheets.Contains(extension))
				{
					return GetImageAsync("Excel");
				}
				
				if (_slides.Contains(extension))
				{
					return GetImageAsync("PowerPoint");
				}
				
				if (_fonts.Contains(extension))
				{
					return GetImageAsync("Font");
				}
				
				if (_access.Contains(extension))
				{
					return GetImageAsync("Access");
				}
				
				if (_xml.Contains(extension))
				{
					return GetImageAsync("Xml");
				}
				
				if (_jpeg.Contains(extension))
				{
					return GetImageAsync("Jpeg");
				}
				
				if (_rawImage.Contains(extension))
				{
					return GetImageAsync("RawImage");
				}

				if (extension is { Length: > 1 })
				{
					return GetImageAsync(extension[1..]);
				}
			}

			return model.IsFolder
				? GetImageAsync("Folder")
				: GetImageAsync("File"); // Task.FromException<IImage?>(new ArgumentException("No image was found for the item"));
		}, size) ?? Task.FromResult<IImage?>(null)).ConfigureAwait(false);
	}

	private static async Task<IImage?> ImageFromFileModel(IFileItem model, int size, Func<bool>? shouldReturnImage)
	{
		return await Runner.RunSecundairy(() => model?.GetPath((path, imageSize) =>
		{
			Bitmap? image = null;

			if (shouldReturnImage is null || shouldReturnImage?.Invoke() == true)
			{
				image = WindowsThumbnailProvider.GetThumbnail(path, imageSize, imageSize, ThumbnailOptions.ThumbnailOnly, () => size is < 64 and >= 32);
			}

			if (image is null && (shouldReturnImage is null || shouldReturnImage?.Invoke() == true))
			{
				image = WindowsThumbnailProvider.GetThumbnail(path, imageSize, imageSize, ThumbnailOptions.IconOnly, () => true);
			}

			return image;
		}, size)).ConfigureAwait(false);
	}

	public static async Task<IImage?> GetFileImage(FileSystemTreeItem? model, int size, Func<bool>? shouldReturnImage = null)
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

				if (image is null && (shouldReturnImage is null || shouldReturnImage?.Invoke() == true))
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
				if (OperatingSystem.IsWindows())
				{
					foreach (var folder in Enum.GetValues<KnownFolder>())
					{
						var folderText = Enum.GetName(folder);

						if (folderText is not null && path.SequenceEqual(KnownFolders.GetPath(folder)))
						{
							return GetImageAsync(folderText);
						}
					}
				}

				if (model.Parent is null)
				{
					var driveInfo = new DriveInfo(new string(model.Value[0], 1));

					if (driveInfo.IsReady)
					{
						return GetImageAsync(Enum.GetName(driveInfo.DriveType));
					}
				}

				if (model.HasChildren)
				{
					return GetImageAsync("FolderFiles");
				}
			}
			else
			{
				var extension = Path.GetExtension(path);

				if (extension is { Length: > 1 })
				{
					return GetImageAsync(extension[1..].ToString().ToLower());
				}
			}

			return model.IsFolder
				? GetImageAsync("Folder")
				: GetImageAsync("File"); // Task.FromException<IImage?>(new ArgumentException("No image was found for the item"));
		}, size) ?? Task.FromResult<IImage?>(null));
	}

	public static async Task<IImage?> GetImageAsync(string key)
	{
		if (String.IsNullOrEmpty(key))
		{
			return null;
		}

		var hash = String.GetHashCode(key);
		
		if (!Images.TryGetValue(hash, out var image))
		{
			var source = SvgSource.Load<SvgSource>($"avares://FileExplorer/Assets/Icons/{key}.svg", null);

			image = await Dispatcher.UIThread.InvokeAsync(() => new SvgImage
			{
				Source = source,
			}, DispatcherPriority.Background).GetTask();
			
			Images.TryAdd(hash, image);
		}

		return image;
	}

	public static IImage? GetImage(ReadOnlySpan<char> key)
	{
		if (key.IsEmpty)
		{
			return null;
		}

		var hash = String.GetHashCode(key);

		if (!Images.TryGetValue(hash, out var image))
		{
			var source = SvgSource.Load<SvgSource>($"avares://FileExplorer/Assets/Icons/{key}.svg", null);

			image = new SvgImage
			{
				Source = source,
			};

			Images.TryAdd(hash, image);
		}

		return image;
	}
}

#pragma warning restore CA1416