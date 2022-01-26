using Avalonia.Media;
using Avalonia.Svg.Skia;
using Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Platform;
using System.Reflection;
using FileTypeAndIcon;
using Avalonia.Threading;

namespace FileExplorerCore.Helpers
{
	public static class ThumbnailProvider
	{
		private static readonly Dictionary<string, string>? fileTypes = OperatingSystem.IsWindows() ? RegisteredFileType.GetFileTypeAndIcon() : new();

		private static readonly Dictionary<string, IImage> Images = new();
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

			//	if (image is { })
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
						    String.Compare(path, KnownFolders.GetPath(folder), StringComparison.OrdinalIgnoreCase) == 0)
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
					name = Directory.EnumerateFileSystemEntries(path, "*", enumerationOptions).Any() ? "FolderFiles" : "Folder";
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

		private static bool ImageExists(string key)
		{
			var loader = AvaloniaLocator.Current.GetService<IAssetLoader>();

			return loader?.Exists(new Uri($"avares://FileExplorerCore/Assets/Icons/{key}.svg")) ?? false;
		}

		private static IImage? GetImage(string key)
		{
			if (!Images.TryGetValue(key, out var image) && image is null && ImageExists(key))
			{
				var source = SvgSource.Load<SvgSource>($"avares://FileExplorerCore/Assets/Icons/{key}.svg", null);
				using var memoryStream = new MemoryStream();

				if (source?.Save(memoryStream, new SkiaSharp.SKColor(0)) == true)
				{
					memoryStream.Seek(0, SeekOrigin.Begin);

					Dispatcher.UIThread.InvokeAsync(() =>
					{
						image = new SvgImage()
						{
							Source = source,
						};

						if (!Images.ContainsKey(key))
						{
							Images.Add(key, image);
						}
					}).Wait();					
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
