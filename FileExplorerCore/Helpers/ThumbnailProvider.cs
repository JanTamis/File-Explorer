using Avalonia.Media;
using Avalonia.Svg.Skia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Threading;
using System.Threading;
using FileExplorerCore.Models;
using Avalonia.Media.Imaging;
using System.Diagnostics.CodeAnalysis;

namespace FileExplorerCore.Helpers;
#pragma warning disable CA1416

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public static class ThumbnailProvider
{
	// private static readonly Dictionary<string, string>? fileTypes = OperatingSystem.IsWindows() ? RegisteredFileType.GetFileTypeAndIcon() : new();

	private static readonly Dictionary<string, SvgImage> Images = new();
	private static readonly Dictionary<string, string[]> TypeMap = new();

	public static readonly ConcurrentExclusiveSchedulerPair concurrentExclusiveScheduler = new(TaskScheduler.Default, Environment.ProcessorCount / 2); // BitOperations.Log2((uint)Environment.ProcessorCount));

	static ThumbnailProvider()
	{
		var assembly = Assembly.GetExecutingAssembly();
		var files = assembly.GetManifestResourceNames();

		const string basePathMapping = "FileExplorerCore.Assets.Lookup.";

		foreach (var file in files)
		{
			if (file.StartsWith(basePathMapping))
			{
				var name = file.Split('.')[^2];
				var stream = assembly.GetManifestResourceStream(file);

				if (stream is not null)
				{
					var reader = new StreamReader(stream);

					TypeMap.Add(name, reader.ReadToEnd()
						.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
				}
			}
		}
	}

	public static async Task<IImage?> GetFileImage(FileSystemTreeItem? treeItem, int size, Func<bool>? shouldReturnImage = null)
	{
		if (treeItem is null || shouldReturnImage is not null && !shouldReturnImage())
		{
			return null;
		}

		if (OperatingSystem.IsWindows())
		{
			return await Task.Factory.StartNew(() => treeItem?.GetPath((path, imageSize) =>
			{
				Bitmap? image = null!;

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

		return await await Task.Factory.StartNew(() => treeItem?.GetPath((path, imageSize) =>
		{
			var name = String.Empty;

			if (treeItem.IsFolder)
			{
				if (OperatingSystem.IsWindows())
				{
					foreach (var folder in Enum.GetValues<KnownFolder>())
					{
						var folderText = Enum.GetName(folder);

						if (folderText is not null && treeItem.GetPath((path, knownFolder) => path.SequenceEqual(KnownFolders.GetPath(knownFolder)), folder))
						{
							name = folderText;
							break;
						}
					}
				}

				if (!treeItem.HasParent && name == String.Empty)
				{
					var driveInfo = new DriveInfo(new string(treeItem.Value[0], 1));

					if (driveInfo.IsReady)
					{
						name = Enum.GetName(driveInfo.DriveType);
					}
				}

				if (name == String.Empty)
				{
					name = treeItem.HasChildren
						? "FolderFiles"
						: "Folder";
				}
			}
			else
			{
				name = "File";

				var extension = Path.GetExtension(treeItem.Value).ToLower();

				if (extension.Length > 1)
				{
					extension = extension[1..];

					if (Images.ContainsKey(extension))
					{
						name = extension;
					}
					else
					{
						foreach (var (key, value) in TypeMap)
						{
							foreach (var val in value)
							{
								if (extension == val)
								{
									name = key;
								}
							}
						}
					}
				}
			}

			return GetImage(name!);
		}, size) ?? Task.FromResult<SvgImage?>(null), CancellationToken.None, TaskCreationOptions.None, concurrentExclusiveScheduler.ExclusiveScheduler).ConfigureAwait(false);
	}

	private static async Task<SvgImage?> GetImage(string key)
	{
		if (key is null or "")
		{
			return null;
		}

		if (!Images.TryGetValue(key, out var image) && image is null)
		{
			var source = SvgSource.Load<SvgSource>($"avares://FileExplorerCore/Assets/Icons/{key}.svg", null);

			if (source is not null)
			{
				using var memoryStream = new MemoryStream();

				if (source.Save(memoryStream, SkiaSharp.SKColors.Transparent))
				{
					memoryStream.Seek(0, SeekOrigin.Begin);

					if (Dispatcher.UIThread.CheckAccess())
					{
						image = new SvgImage()
						{
							Source = source,
						};

						if (!Images.ContainsKey(key))
						{
							Images.Add(key, image);
						}
					}
					else
					{
						await Dispatcher.UIThread.InvokeAsync(() =>
						{
							image = new SvgImage()
							{
								Source = source,
							};

							if (!Images.ContainsKey(key))
							{
								Images.Add(key, image);
							}
						});
					}
				}
			}
		}

		return image;
	}
}
#pragma warning restore CA1416