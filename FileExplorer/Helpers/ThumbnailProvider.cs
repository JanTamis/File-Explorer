using Avalonia.Media;
using Avalonia.Svg.Skia;
using System.IO;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using System.Diagnostics.CodeAnalysis;
using FileExplorer.Core.Interfaces;
using FileExplorer.Models;

namespace FileExplorer.Helpers;
#pragma warning disable CA1416

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public static class ThumbnailProvider
{
	// private static readonly Dictionary<string, string>? fileTypes = OperatingSystem.IsWindows() ? RegisteredFileType.GetFileTypeAndIcon() : new();

	private static readonly Dictionary<string, IImage> Images = new();
	// private static readonly ConcurrentDictionary<string, string[]> TypeMap = new();

	public static readonly ConcurrentExclusiveSchedulerPair concurrentExclusiveScheduler = new(TaskScheduler.Default, Environment.ProcessorCount / 2); // BitOperations.Log2((uint)Environment.ProcessorCount));

	static ThumbnailProvider()
	{
		//if (!OperatingSystem.IsWindows())
		//{
			//var assembly = Assembly.GetExecutingAssembly();
			//var files = assembly.GetManifestResourceNames();

			//const string basePathMapping = "FileExplorer.Assets.Lookup.";

			//foreach (var file in files)
			//{
			//	if (file.StartsWith(basePathMapping))
			//	{
			//		var name = file.Split('.')[^2];
			//		var stream = assembly.GetManifestResourceStream(file);

			//		if (stream is not null)
			//		{
			//			var reader = new StreamReader(stream);

			//			reader.ReadToEndAsync().ContinueWith(c =>
			//			{
			//				TypeMap.TryAdd(name, reader.ReadToEnd()
			//					.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
			//			});
			//		}
			//	}
			//}
		//}
	}

	public static async Task<IImage?> GetFileImage(IFileItem? model, IItemProvider provider, int size, Func<bool>? shouldReturnImage = null)
	{
		if (model is null || shouldReturnImage is not null && !shouldReturnImage())
		{
			return null;
		}

		if (OperatingSystem.IsWindows() && model is FileModel)
		{
			return await Task.Factory.StartNew(() => model?.GetPath((path, imageSize) =>
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
			}, size), CancellationToken.None, TaskCreationOptions.DenyChildAttach, concurrentExclusiveScheduler.ConcurrentScheduler).ConfigureAwait(false);
		}

		return await await Task.Factory.StartNew(() => model?.GetPath((path, imageSize) =>
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
							return GetImage(folderText);
						}
					}
				}

				if (model is FileModel { IsRoot: false })
				{
					var driveInfo = new DriveInfo(new string(model.Name[0], 1));

					if (driveInfo.IsReady)
					{
						return GetImage(Enum.GetName(driveInfo.DriveType));
					}
				}

				if (provider.HasItems(model))
				{
					return GetImage("FolderFiles");
				}
			}
			else
			{
				var extension = model.Extension?.ToLower();

				if (extension is { Length: > 1 })
				{
					return GetImage(extension[1..]);
				}
			}

			return Task.FromException<IImage?>(new ArgumentException("No image was found for the item"));
		}, size) ?? Task.FromResult<IImage?>(null), CancellationToken.None, TaskCreationOptions.None, concurrentExclusiveScheduler.ExclusiveScheduler).ConfigureAwait(false);
	}

	public static async Task<IImage?> GetFileImage(FileSystemTreeItem? model, int size, Func<bool>? shouldReturnImage = null)
	{
		if (model is null || shouldReturnImage is not null && !shouldReturnImage())
		{
			return null;
		}

		if (OperatingSystem.IsWindows())
		{
			return await Task.Factory.StartNew(() => model?.GetPath((path, imageSize) =>
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
			}, size), CancellationToken.None, TaskCreationOptions.DenyChildAttach, concurrentExclusiveScheduler.ConcurrentScheduler).ConfigureAwait(false);
		}

		return await await Task.Factory.StartNew(() => model?.GetPath((path, imageSize) =>
		{
			var name = String.Empty;

			if (model.IsFolder)
			{
				foreach (var folder in Enum.GetValues<Environment.SpecialFolder>())
				{
					var folderText = Enum.GetName(folder);

					if (folderText is not null && model.GetPath((path, knownFolder) => path.SequenceEqual(Environment.GetFolderPath(knownFolder)), folder))
					{
						name = folderText;
						break;
					}
				}

				if (name == String.Empty)
				{
					var drives = DriveInfo.GetDrives();

					foreach (var drive in drives)
					{
						if (drive.IsReady && drive.RootDirectory.FullName == path)
						{
							name = "Fixed";
							break;
						}
					}
				}

				if (name == String.Empty)
				{
					name = model.HasChildren
						? "FolderFiles"
						: "Folder";
				}
			}
			else
			{
				// name = "File";

				name = Path.GetExtension(model.Value).ToLower();

				//if (extension.Length > 1)
				//{
				//	extension = extension[1..];

				//	if (Images.ContainsKey(extension))
				//	{
				//		name = extension;
				//	}
				//	else
				//	{
				//		foreach (var (key, value) in TypeMap)
				//		{
				//			if (value.Contains(extension))
				//			{
				//				name = key;
				//			}
				//		}
				//	}
				//}
			}

			return GetImage(name);
		}, size) ?? Task.FromResult<IImage?>(null), CancellationToken.None, TaskCreationOptions.None, concurrentExclusiveScheduler.ExclusiveScheduler).ConfigureAwait(false);
	}

	private static async Task<IImage?> GetImage(string? key)
	{
		if (String.IsNullOrEmpty(key))
		{
			return null;
		}

		if (!Images.TryGetValue(key, out var image))
		{
			var source = SvgSource.Load<SvgSource>($"avares://FileExplorer/Assets/Icons/{key}.svg", null);

			image = await Dispatcher.UIThread.InvokeAsync(() => new SvgImage
			{
				Source = source,
			}, DispatcherPriority.Background);

			Images.TryAdd(key, image);
		}

		return image;
	}
}

#pragma warning restore CA1416