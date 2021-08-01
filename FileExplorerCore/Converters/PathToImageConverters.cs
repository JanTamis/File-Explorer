using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using FileExplorerCore.Helpers;
using FileTypeAndIcon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace FileExplorerCore.Converters
{
	public class PathToImageConverter : IValueConverter
	{
		readonly static bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

		static readonly Dictionary<string, string> fileTypes = RegisteredFileType.GetFileTypeAndIcon();

		readonly Dictionary<string, IImage> Images = new();
		readonly Dictionary<string, string[]> TypeMap = new();

		readonly EnumerationOptions enumerationOptions = new()
		{
			AttributesToSkip = FileAttributes.Hidden
		};

		public PathToImageConverter()
		{
			if (!IsWindows)
			{
				var assembly = Assembly.GetExecutingAssembly();
				var files = assembly.GetManifestResourceNames();

				var basePathMapping = "FileExplorerCore.Assets.Lookup.";

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
																			.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
						}
					}
				}
			}
		}

		public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var name = String.Empty;

			if (value is string path)
			{
				if (IsWindows)
				{
					var image = WindowsThumbnailProvider.GetThumbnail(path, 48, 48, ThumbnailOptions.None);

					if (image is { })
					{
						return image;
					}
				}

				if (Directory.Exists(path))
				{
					path = Path.GetFullPath(path);

					foreach (var folder in Enum.GetValues<KnownFolder>())
					{
						var folderText = folder.ToString();
						path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

						if (ImageExists(folderText) && path.ToUpper() == KnownFolders.GetPath(folder).ToUpper())
						{
							name = folderText;
						}
					}

					if (name == String.Empty)
					{
						var driveInfos = DriveInfo.GetDrives();

						foreach (var drive in driveInfos)
						{
							if (drive.IsReady && drive.Name == path)
							{
								name = drive.DriveType.ToString();
							}
						}
					}

					if (name == String.Empty)
					{
						if (Directory.EnumerateFileSystemEntries(path, "*", enumerationOptions).Any())
						{
							name = "FolderFiles";
						}
						else
						{
							name = "Folder";
						}
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
							foreach (var item in TypeMap)
							{
								for (int i = 0; i < item.Value.Length; i++)
								{
									if (extension == item.Value[i])
									{
										name = item.Key;
									}
								}
							}
						}
					}
				}
			}

			if (!String.IsNullOrEmpty(name))
			{
				return GetImage(name);
			}

			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		private bool ImageExists(string key)
		{
			var loader = AvaloniaLocator.Current.GetService<IAssetLoader>();

			return loader.Exists(new Uri($"avares://FileExplorerCore/Assets/Icons/{key}.svg"));
		}

		private IImage? GetImage(string key)
		{
			if (!Images.TryGetValue(key, out IImage? image))
			{
				using var source = SvgSource.Load<SvgSource>($"avares://FileExplorerCore/Assets/Icons/{key}.svg", null);
				using var memoryStream = new MemoryStream();

				if (source.Save(memoryStream, new SkiaSharp.SKColor(0)))
				{
					memoryStream.Seek(0, SeekOrigin.Begin);
					image = new Bitmap(memoryStream);

					if (!Images.ContainsKey(key))
					{
						Images.Add(key, image);
					}
				}
			}

			return image;
		}

		private string GetOfficeName(string extension, string defaultName) => extension switch
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
