using Avalonia.Controls;
using Avalonia.Data.Converters;
using Material.Icons;
using System.Globalization;

namespace FileExplorer.Converters;

public sealed class WindowStateToIconConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value switch
		{
			WindowState.Maximized => MaterialIconKind.FullscreenExit,
			WindowState.FullScreen => MaterialIconKind.FullscreenExit,
			_ => MaterialIconKind.Fullscreen,
		};
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}