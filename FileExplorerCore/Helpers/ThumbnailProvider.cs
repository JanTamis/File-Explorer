﻿using Avalonia.Media;
using Avalonia.Svg.Skia;
using Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Platform;
using System.Reflection;
using System.Threading.Tasks;
using FileTypeAndIcon;
using Avalonia.Threading;
using FileExplorerCore.ViewModels;
using System.Diagnostics;

namespace FileExplorerCore.Helpers
{
	public static class ThumbnailProvider
	{
		private static readonly Dictionary<string, string>? fileTypes = OperatingSystem.IsWindows() ? RegisteredFileType.GetFileTypeAndIcon() : new();

		private static readonly Dictionary<string, SvgImage> Images = new();
		private static readonly Dictionary<string, string[]> TypeMap = new();

		private static readonly EnumerationOptions enumerationOptions = new()
		{
			AttributesToSkip = FileAttributes.Hidden,
		};

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

					if (stream is { })
					{
						var reader = new StreamReader(stream);

						TypeMap.Add(name, reader.ReadToEnd()
							.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
					}
				}
			}
		}

		public static IImage? GetFileImage(string? path)
		{
			if (path is null)
			{
				return null;
			}

			var name = String.Empty;

			//if (OperatingSystem.IsWindows())
			//{
			//	var image = WindowsThumbnailProvider.GetThumbnail(path, 64, 64);

			//	if (image is not null)
			//	{
			//		return image;
			//	}
			//}

			if (Directory.Exists(path))
			{
				path = Path.GetFullPath(path);

				if (OperatingSystem.IsWindows())
				{
					foreach (var folder in Enum.GetValues<KnownFolder>())
					{
						var folderText = Enum.GetName(folder);
						path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

						if (folderText is not null && ImageExists(folderText) &&
								String.Compare(path, KnownFolders.GetPath(folder).ToString(), StringComparison.OrdinalIgnoreCase) == 0)
						{
							name = folderText;
						}
					}
				}

				if (name == String.Empty)
				{
					var driveInfos = DriveInfo.GetDrives();

					foreach (var drive in driveInfos)
					{
						if (drive.IsReady && drive.Name == path)
						{
							name = Enum.GetName(drive.DriveType);
						}
					}
				}

				if (name == String.Empty)
				{
					var item = TabItemViewModel.GetTreeItem(path);

					name = item.HasChildren
						? "FolderFiles"
						: "Folder";
				}
			}
			else if (File.Exists(path))
			{
				name = "File";

				var extension = Path.GetExtension(path).ToLower();

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
		}

		public static IImage? GetFileImage(FileSystemTreeItem? treeItem)
		{
			if (treeItem is null)
			{
				return null;
			}

			var name = String.Empty;

			//if (OperatingSystem.IsWindows())
			//{
			//	var image = treeItem.GetPath(path => WindowsThumbnailProvider.GetThumbnail(path, 64, 64));

			//	if (image is not null)
			//	{
			//		return image;
			//	}
			//}

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
						}
					}
				}


				if (!treeItem.HasParent)
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
		}

		private static bool ImageExists(string key)
		{
			var loader = AvaloniaLocator.Current.GetService<IAssetLoader>();

			return loader?.Exists(new Uri($"avares://FileExplorerCore/Assets/Icons/{key}.svg")) ?? false;
		}

		private static SvgImage? GetImage(string key)
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

						image = new SvgImage()
						{
							Source = source,
						};

						if (!Images.ContainsKey(key))
						{
							Images.Add(key, image);
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
			_ => defaultName
		};
	}
}
