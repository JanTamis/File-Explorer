using System.Globalization;
using Avalonia.Data.Converters;
using FileExplorer.Interfaces;
using FileExplorer.Resources;
using Humanizer;

namespace FileExplorer.Converters;

public sealed class EnumToTextConverter : IValueConverter, ISingleton<EnumToTextConverter>
{
	public static EnumToTextConverter Instance { get; } = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is null)
		{
			return String.Empty;
		}
		
		var name = value.ToString() ?? String.Empty;
		return ResourceDefault.ResourceManager.GetString(name) ?? name.Humanize() ?? String.Empty;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}