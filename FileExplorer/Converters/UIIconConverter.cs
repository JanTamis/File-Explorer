using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Svg.Skia;
using FileExplorer.Interfaces;

namespace FileExplorer.Converters;

public class UIIconConverter : IValueConverter, ISingleton<UIIconConverter>
{
	public static readonly UIIconConverter Instance = new();	
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var path = $"avares://FileExplorer/Assets/UIIcons/{value}.svg";
		var source = SvgSource.Load<SvgSource>(path, null);

		return new SvgImage { Source = source };
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}