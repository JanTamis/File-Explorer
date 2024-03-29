using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Svg.Skia;
using FileExplorer.Interfaces;

namespace FileExplorer.Converters;

public sealed class EnumToIconConverter : IValueConverter, ISingleton<EnumToIconConverter>
{
	public static EnumToIconConverter Instance { get; } = new();
	
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is Enum && parameter is Type type)
		{
			try
			{
				var name = Enum.GetName(type, value);
				var source = SvgSource.Load<SvgSource>($"avares://FileExplorer/Assets/UIIcons/{name}.svg", null);

				return new SvgImage
				{
					Source = source
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