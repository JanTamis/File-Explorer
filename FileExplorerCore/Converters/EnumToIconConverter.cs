using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Svg.Skia;
using FileExplorerCore.Interfaces;

namespace FileExplorerCore.Converters;

public class EnumToIconConverter : IValueConverter, ISingleton<EnumToIconConverter>
{
	public static readonly EnumToIconConverter Instance = new();
	
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is Enum && parameter is Type type)
		{
			try
			{
				var name = Enum.GetName(type, value);
				var source = SvgSource.Load<SvgSource>($"avares://FileExplorerCore/Assets/UIIcons/{name}.svg", null);

				return new SvgImage
				{
					Source = source,
				};
			}
			catch (FileNotFoundException) { }			
		}

		return null;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}