using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Svg.Skia;
using FileExplorer.Interfaces;

namespace FileExplorer.Converters;

public class UIIconConverter : IValueConverter, ISingleton<UIIconConverter>
{
	public static UIIconConverter Instance { get; } = new();
	
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return new SvgImage
		{
			Source = SvgSource.Load<SvgSource>($"avares://FileExplorer/Assets/UIIcons/{value}.svg", null),
		};
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}