using Avalonia.Media;
using Avalonia.Svg.Skia;
using Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Platform;
using System.Reflection;
using System.Threading.Tasks;
using FileTypeAndIcon;
using Avalonia.Threading;
using System.Threading;
using FileExplorerCore.Models;

namespace FileExplorerCore.Helpers
{
	public static class ThumbnailProvider
	{
		private static readonly Dictionary<string, string>? fileTypes = OperatingSystem.IsWindows() ? RegisteredFileType.GetFileTypeAndIcon() : new();

		private static readonly Dictionary<string, SvgImage> Images = new();
		private static readonly Dictionary<string, string[]> TypeMap = new();

		private static readonly ConcurrentExclusiveSchedulerPair concurrentExclusiveScheduler = new(TaskScheduler.Default);

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
			if (treeItem is null || (shouldReturnImage is not null && !shouldReturnImage()))
			{
				return null;
			}

			if (OperatingSystem.IsWindows())
			{
				return await Task.Factory.StartNew(() => treeItem?.GetPath((path, imageSize) =>
				{
					IImage? image = null;

					if (shouldReturnImage is null || shouldReturnImage?.Invoke() == true)
					{
						if (treeItem.IsFolder && treeItem.HasChildren)
						{
							image = WindowsThumbnailProvider.GetThumbnail(path, imageSize, imageSize, ThumbnailOptions.ThumbnailOnly);
						}

						if (image is null)
						{
							image = WindowsThumbnailProvider.GetThumbnail(path, imageSize, imageSize, ThumbnailOptions.IconOnly);
						}
					}

					return image;
				}, size), CancellationToken.None, TaskCreationOptions.None, concurrentExclusiveScheduler.ExclusiveScheduler).ConfigureAwait(false);
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

							if (folderText is not null && treeItem.GetPath(x => x.SequenceEqual(KnownFolders.GetPath(folder))))
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

				return GetImage(name);
			}, size), CancellationToken.None, TaskCreationOptions.None, concurrentExclusiveScheduler.ExclusiveScheduler).ConfigureAwait(false);

		}

		private static bool ImageExists(string key)
		{
			var loader = AvaloniaLocator.Current.GetService<IAssetLoader>();

			return loader?.Exists(new Uri($"avares://FileExplorerCore/Assets/Icons/{key}.svg")) ?? false;
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

					if (source.Save(memoryStream, SkiaSharp.SKColors.Transparent) == true)
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

		private static string GetOfficeName(string extension, string defaultName) => extension switch
		{
			"doc" or
			"docm" or
			"docx" or
			"dotm" or
			"dotx" or
			"odt" or
			"wps" => "Word",

			"odp" or
			"pot" or
			"potm" or
			"potx" or
			"ppa" or
			"ppam" or
			"pps" or
			"ppsm" or
			"ppsx" or
			"ppt" or
			"pptm" or
			"pptx" or
			"thmx" => "PowerPoint",

			"csv" or
			"dbf" or
			"dif" or
			"ods" or
			"prn" or
			"slk" or
			"xla" or
			"xla" or
			"xlam" or
			"xls" or
			"xlsb" or
			"xlsm" or
			"xlsx" or
			"xlt" or
			"xltm" or
			"xltx" or
			"xlw" => "Excel",
			_ => defaultName
		};
	}
}