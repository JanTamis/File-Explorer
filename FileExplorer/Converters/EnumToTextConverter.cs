using System.Globalization;
using Avalonia.Data.Converters;
using FileExplorer.Interfaces;
using FileExplorer.Resources;
using Humanizer;

namespace FileExplorer.Converters;

public sealed class EnumToTextConverter : IValueConverter, ISingleton<EnumToTextConverter>
{
	public static readonly EnumToTextConverter Instance = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var name = value?.ToString();
		return ResourceDefault.ResourceManager.GetString(name!) ?? value?.ToString().Humanize() ?? String.Empty;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}